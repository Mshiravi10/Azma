$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $Root

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"

$LogDir = Join-Path $Root "artifacts\logs"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$AppPorts = @(8080, 5201, 5202, 5203, 5204, 5205, 5206, 5173)
$Started = @{}

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message"
}

function Invoke-Checked {
    param(
        [string]$File,
        [string[]]$Arguments,
        [string]$FailureMessage
    )

    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

function Get-ProcessCommandLine {
    param([int]$ProcessId)

    try {
        $process = Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction Stop
        return [pscustomobject]@{
            Name = $process.Name
            CommandLine = [string]$process.CommandLine
        }
    } catch {
        return [pscustomobject]@{
            Name = ""
            CommandLine = ""
        }
    }
}

function Test-IsAzmaProcess {
    param(
        [string]$Name,
        [string]$CommandLine
    )

    if ($Name -like "Azma.*.exe") { return $true }
    if ($CommandLine -like "*$Root*") { return $true }
    if ($CommandLine -match "Azma\.") { return $true }
    if ($CommandLine -match "vite[\\/]+bin[\\/]+vite|node_modules[\\/]+\.bin|npm(\.cmd)?\s+run\s+dev") { return $true }
    return $false
}

function Stop-AzmaProcessesOnPorts {
    $blocked = New-Object System.Collections.Generic.List[string]

    foreach ($port in $AppPorts) {
        $processIds = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique

        foreach ($processId in $processIds) {
            if (-not $processId -or $processId -eq $PID) { continue }

            $info = Get-ProcessCommandLine -ProcessId $processId
            if (Test-IsAzmaProcess -Name $info.Name -CommandLine $info.CommandLine) {
                Write-Host "Stopping stale Azma process PID $processId on port $port"
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            } else {
                $blocked.Add("Port $port is already used by PID $processId ($($info.Name)): $($info.CommandLine)")
            }
        }
    }

    if ($blocked.Count -gt 0) {
        $blocked | ForEach-Object { Write-Error $_ }
        throw "One or more Azma ports are used by non-Azma processes."
    }

    Start-Sleep -Seconds 1
}

function Assert-PortsAreNotExcluded {
    $excluded = @(netsh interface ipv4 show excludedportrange protocol=tcp 2>$null) +
        @(netsh interface ipv6 show excludedportrange protocol=tcp 2>$null)

    foreach ($port in $AppPorts) {
        foreach ($line in $excluded) {
            if ($line -match "^\s*(\d+)\s+(\d+)\s*\*?\s*$") {
                $start = [int]$Matches[1]
                $end = [int]$Matches[2]
                if ($port -ge $start -and $port -le $end) {
                    throw "Port $port is reserved by Windows TCP excluded port range $start-$end. Change Azma local ports or release that Windows reservation."
                }
            }
        }
    }
}

function Test-IsPrivateIPv4Address {
    param([string]$IPAddress)

    return $IPAddress -match "^10\." -or
        $IPAddress -match "^192\.168\." -or
        $IPAddress -match "^172\.(1[6-9]|2\d|3[0-1])\."
}

function Get-PrivateIPv4Addresses {
    try {
        return @(
            Get-NetIPAddress -AddressFamily IPv4 -ErrorAction Stop |
                Where-Object {
                    $_.IPAddress -and
                    $_.IPAddress -ne "127.0.0.1" -and
                    (Test-IsPrivateIPv4Address -IPAddress $_.IPAddress)
                } |
                Select-Object -ExpandProperty IPAddress -Unique |
                Sort-Object
        )
    } catch {
        return @()
    }
}

function Stop-StaleStarterProcesses {
    $patterns = @(
        "dotnet(\.exe)?[`" ]+\s*run\s+--project\s+src[\\/]+(Gateway|Services)",
        "vite(\.js)?\s+--host",
        "npm(\.cmd)?\s+run\s+dev"
    )

    Get-CimInstance Win32_Process |
        Where-Object {
            $commandLine = $_.CommandLine
            $_.ProcessId -ne $PID -and
            $commandLine -and
            (($patterns | Where-Object { $commandLine -match $_ }).Count -gt 0)
        } |
        ForEach-Object {
            if (Test-IsAzmaProcess -Name $_.Name -CommandLine $_.CommandLine) {
                Write-Host "Stopping stale starter process PID $($_.ProcessId)"
                Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
            }
        }
}

function Wait-DockerHealth {
    param([string[]]$Names, [int]$TimeoutSeconds = 90)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $notReady = @()
        foreach ($name in $Names) {
            $state = docker inspect -f "{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{end}}" $name 2>$null
            if ($LASTEXITCODE -ne 0) {
                $notReady += "${name}:not-found"
                continue
            }

            $parts = "$state".Trim().Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)
            $status = if ($parts.Count -gt 0) { $parts[0] } else { "" }
            $health = if ($parts.Count -gt 1) { $parts[1] } else { "" }

            if ($status -ne "running" -or ($health -and $health -ne "healthy")) {
                $notReady += "${name}:$state"
            }
        }

        if ($notReady.Count -eq 0) { return }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "Infrastructure is not healthy: $($notReady -join ', ')"
}

function Start-AzmaProcess {
    param(
        [string]$Name,
        [string]$File,
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )

    $outLog = Join-Path $LogDir "$Name.out.log"
    $errLog = Join-Path $LogDir "$Name.err.log"
    Remove-Item -LiteralPath $outLog, $errLog -Force -ErrorAction SilentlyContinue

    $process = Start-Process `
        -FilePath $File `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -WindowStyle Hidden `
        -RedirectStandardOutput $outLog `
        -RedirectStandardError $errLog `
        -PassThru

    $Started[$Name] = [pscustomobject]@{
        Process = $process
        OutLog = $outLog
        ErrLog = $errLog
    }

    Write-Host ("Started {0,-10} PID {1}" -f $Name, $process.Id)
}

function Show-LogTail {
    param([string]$Name)

    if (-not $Started.ContainsKey($Name)) { return }
    $entry = $Started[$Name]

    foreach ($log in @($entry.ErrLog, $entry.OutLog)) {
        if (Test-Path $log) {
            Write-Host ""
            Write-Host "--- $Name log: $([System.IO.Path]::GetFileName($log)) ---"
            Get-Content -LiteralPath $log -Tail 80 -ErrorAction SilentlyContinue
        }
    }
}

function Wait-Http {
    param(
        [string]$Name,
        [string]$Uri,
        [int]$TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        if ($Started.ContainsKey($Name)) {
            $process = $Started[$Name].Process
            if ($process.HasExited) {
                Show-LogTail -Name $Name
                throw "$Name exited before it became healthy."
            }
        }

        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Host ("Healthy {0,-10} {1}" -f $Name, $Uri)
                return
            }
        } catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)

    Show-LogTail -Name $Name
    throw "$Name did not become healthy at $Uri within $TimeoutSeconds seconds."
}

Write-Host "Azma Test Management Platform"
Write-Host "Root: $Root"

Write-Step "Cleaning stale local Azma processes"
Assert-PortsAreNotExcluded
Stop-StaleStarterProcesses
Stop-AzmaProcessesOnPorts

Write-Step "Checking Docker"
Invoke-Checked -File "docker" -Arguments @("info") -FailureMessage "Docker is not running. Start Docker Desktop first."

Write-Step "Starting infrastructure"
Invoke-Checked -File "docker" -Arguments @("compose", "-f", "deploy/docker-compose.yml", "up", "-d") -FailureMessage "Failed to start Docker infrastructure."
Wait-DockerHealth -Names @("azma-postgres", "azma-redis", "azma-rabbitmq", "azma-minio")

Write-Step "Building .NET solution"
Invoke-Checked -File "dotnet" -Arguments @("build", "Azma.sln", "--nologo") -FailureMessage "Failed to build Azma .NET solution."

Write-Step "Ensuring Playwright runtime"
$PlaywrightInstaller = Join-Path $Root "src\Services\Automation\Azma.Automation.Api\bin\Debug\net8.0\playwright.ps1"
if (-not (Test-Path -LiteralPath $PlaywrightInstaller)) {
    throw "Playwright installer was not generated at $PlaywrightInstaller. Build the Automation service first."
}
Invoke-Checked -File "powershell.exe" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $PlaywrightInstaller, "install", "chromium") -FailureMessage "Failed to install Playwright Chromium runtime."

Write-Step "Building recorder extension"
Invoke-Checked -File "npm.cmd" -Arguments @("run", "build", "--prefix", "src/Web/azma-recorder-extension") -FailureMessage "Failed to build Azma Recorder extension."

Write-Step "Starting backend microservices"
Start-AzmaProcess -Name "identity" -File "dotnet" -Arguments @("run", "--no-build", "--project", "src/Services/Identity/Azma.Identity.Api") -WorkingDirectory $Root
Start-AzmaProcess -Name "projects" -File "dotnet" -Arguments @("run", "--no-build", "--project", "src/Services/Projects/Azma.Projects.Api") -WorkingDirectory $Root
Start-AzmaProcess -Name "testcases" -File "dotnet" -Arguments @("run", "--no-build", "--project", "src/Services/TestCases/Azma.TestCases.Api") -WorkingDirectory $Root
Start-AzmaProcess -Name "runs" -File "dotnet" -Arguments @("run", "--no-build", "--project", "src/Services/Runs/Azma.Runs.Api") -WorkingDirectory $Root
Start-AzmaProcess -Name "reporting" -File "dotnet" -Arguments @("run", "--no-build", "--project", "src/Services/Reporting/Azma.Reporting.Api") -WorkingDirectory $Root
Start-AzmaProcess -Name "automation" -File "dotnet" -Arguments @("run", "--no-build", "--project", "src/Services/Automation/Azma.Automation.Api", "--urls", "http://localhost:5206") -WorkingDirectory $Root

Wait-Http -Name "identity" -Uri "http://localhost:5201/health"
Wait-Http -Name "projects" -Uri "http://localhost:5202/health"
Wait-Http -Name "testcases" -Uri "http://localhost:5203/health"
Wait-Http -Name "runs" -Uri "http://localhost:5204/health"
Wait-Http -Name "reporting" -Uri "http://localhost:5205/health"
Wait-Http -Name "automation" -Uri "http://localhost:5206/health"

Write-Step "Starting gateway and web"
Start-AzmaProcess -Name "gateway" -File "dotnet" -Arguments @("run", "--no-build", "--project", "src/Gateway/Azma.Gateway") -WorkingDirectory $Root
Start-AzmaProcess -Name "web" -File "npm.cmd" -Arguments @("run", "dev", "--", "--host", "0.0.0.0") -WorkingDirectory (Join-Path $Root "src/Web/azma-web")

Wait-Http -Name "gateway" -Uri "http://localhost:8080/health"
Wait-Http -Name "web" -Uri "http://localhost:5173"

Write-Step "All services are ready"
Write-Host "Web:       http://localhost:5173"
Write-Host "Gateway:   http://localhost:8080"
Write-Host "MCP:       http://localhost:8080/mcp"
Write-Host "Identity:  http://localhost:5201"
Write-Host "Projects:  http://localhost:5202"
Write-Host "TestCases: http://localhost:5203"
Write-Host "Runs:      http://localhost:5204"
Write-Host "Reporting: http://localhost:5205"
Write-Host "Automation: http://localhost:5206"
Write-Host "Recorder Extension Chrome:  src/Web/azma-recorder-extension/dist/chrome"
Write-Host "Recorder Extension Firefox: src/Web/azma-recorder-extension/dist/firefox"
Write-Host "Logs:      $LogDir"

$lanAddresses = Get-PrivateIPv4Addresses
if ($lanAddresses.Count -gt 0) {
    Write-Host ""
    Write-Host "LAN access:"
    foreach ($ip in $lanAddresses) {
        Write-Host "Web:       http://${ip}:5173"
        Write-Host "Gateway:   http://${ip}:8080"
        Write-Host "MCP:       http://${ip}:8080/mcp"
    }
}
