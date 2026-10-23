$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $Root

$AppPorts = @(8080, 5201, 5202, 5203, 5204, 5205, 5206, 5173)

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

Write-Host "Stopping local Azma app processes..."

foreach ($port in $AppPorts) {
    $processIds = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique

    foreach ($processId in $processIds) {
        if (-not $processId -or $processId -eq $PID) { continue }

        $info = Get-ProcessCommandLine -ProcessId $processId
        if (Test-IsAzmaProcess -Name $info.Name -CommandLine $info.CommandLine) {
            Write-Host "Stopping PID $processId on port $port"
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        } else {
            Write-Warning "Port $port is used by non-Azma PID $processId ($($info.Name)); leaving it alone."
        }
    }
}

Get-CimInstance Win32_Process |
    Where-Object {
        $commandLine = $_.CommandLine
        $_.ProcessId -ne $PID -and
        $commandLine -and
        ($commandLine -match "dotnet(\.exe)?[`" ]+\s*run\s+--project\s+src[\\/]+(Gateway|Services)|vite(\.js)?\s+--host|npm(\.cmd)?\s+run\s+dev")
    } |
    ForEach-Object {
        if (Test-IsAzmaProcess -Name $_.Name -CommandLine $_.CommandLine) {
            Write-Host "Stopping starter PID $($_.ProcessId)"
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
        }
    }

Write-Host "Stopping Docker infrastructure..."
docker compose -f deploy/docker-compose.yml down
if ($LASTEXITCODE -ne 0) {
    throw "Failed to stop Docker infrastructure."
}

Write-Host "All services stopped."
