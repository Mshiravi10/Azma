$ErrorActionPreference = 'Stop'
$env:HTTP_PROXY = $null; $env:HTTPS_PROXY = $null
$base = 'http://localhost:8080'

$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{ email = 'ali@azma.local'; password = 'Password123!' } | ConvertTo-Json)
$token = $login.accessToken
$ws = $login.workspaces[0].id
$headers = @{ Authorization = "Bearer $token"; 'X-Workspace-Id' = $ws }
"login ok ws=$ws"

$runs = Invoke-RestMethod -Uri "$base/api/runs?page=1&pageSize=5" -Headers $headers
$runId = $runs.items[0].id
"runId=$runId"

$cases = Invoke-RestMethod -Uri "$base/api/runs/$runId/cases?pageSize=5" -Headers $headers
$rcid = $cases.items[0].id
"rcid=$rcid"

$tmp = Join-Path $env:TEMP 'azma-att-test.txt'
Set-Content -Path $tmp -Value "hello azma $(Get-Date -Format o)"
"file=$tmp size=$((Get-Item $tmp).Length)"

$up = Invoke-RestMethod -Uri "$base/api/runs/$runId/attachments" -Headers $headers -Method Post -Form @{ file = Get-Item $tmp; runCaseId = $rcid }
"uploaded id=$($up.id) name=$($up.fileName) size=$($up.sizeBytes)"

$list = Invoke-RestMethod -Uri "$base/api/runs/$runId/attachments?runCaseId=$rcid" -Headers $headers
"list count=$($list.items.Count)"

$urlR = Invoke-RestMethod -Uri "$base/api/attachments/$($up.id)/url" -Headers $headers
"url(80)=$($urlR.url.Substring(0, [Math]::Min(80, $urlR.url.Length)))..."

$content = Invoke-RestMethod -Uri $urlR.url
"downloaded='$content'"

Invoke-RestMethod -Uri "$base/api/attachments/$($up.id)" -Headers $headers -Method Delete | Out-Null
"deleted ok"

$listAfter = Invoke-RestMethod -Uri "$base/api/runs/$runId/attachments?runCaseId=$rcid" -Headers $headers
"list after=$($listAfter.items.Count)"
