$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Dist = Join-Path $Root "dist"

if (!(Test-Path (Join-Path $Dist "chrome"))) {
  & node (Join-Path $PSScriptRoot "build.mjs")
}

$ChromeZip = Join-Path $Dist "azma-recorder-chrome.zip"
$FirefoxZip = Join-Path $Dist "azma-recorder-firefox.zip"
Remove-Item $ChromeZip, $FirefoxZip -Force -ErrorAction SilentlyContinue

Compress-Archive -Path (Join-Path $Dist "chrome\*") -DestinationPath $ChromeZip -Force
Compress-Archive -Path (Join-Path $Dist "firefox\*") -DestinationPath $FirefoxZip -Force

$WebDownloads = Join-Path $Root "..\azma-web\public\downloads\recorder"
New-Item -ItemType Directory -Path $WebDownloads -Force | Out-Null

$PublishedChromeZip = Join-Path $WebDownloads "azma-recorder-chrome.zip"
$PublishedFirefoxZip = Join-Path $WebDownloads "azma-recorder-firefox.zip"
Copy-Item -LiteralPath $ChromeZip -Destination $PublishedChromeZip -Force
Copy-Item -LiteralPath $FirefoxZip -Destination $PublishedFirefoxZip -Force

function Get-Sha256Hex([string] $Path) {
  $Hasher = [System.Security.Cryptography.SHA256]::Create()
  try {
    $Bytes = [System.IO.File]::ReadAllBytes($Path)
    return ([System.BitConverter]::ToString($Hasher.ComputeHash($Bytes))).Replace("-", "").ToLowerInvariant()
  }
  finally {
    $Hasher.Dispose()
  }
}

$ChromeHash = Get-Sha256Hex $PublishedChromeZip
$FirefoxHash = Get-Sha256Hex $PublishedFirefoxZip
Set-Content -LiteralPath (Join-Path $WebDownloads "azma-recorder-chrome.zip.sha256") -Value "$ChromeHash  azma-recorder-chrome.zip" -Encoding ascii
Set-Content -LiteralPath (Join-Path $WebDownloads "azma-recorder-firefox.zip.sha256") -Value "$FirefoxHash  azma-recorder-firefox.zip" -Encoding ascii

Write-Host "Created:"
Write-Host " - $ChromeZip"
Write-Host " - $FirefoxZip"
Write-Host "Published for web download:"
Write-Host " - $PublishedChromeZip"
Write-Host " - $PublishedFirefoxZip"
