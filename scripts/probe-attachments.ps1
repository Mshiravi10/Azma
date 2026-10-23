$urls = @(
    'http://localhost:8080/api/runs/00000000-0000-0000-0000-000000000000/attachments',
    'http://localhost:8080/api/attachments/00000000-0000-0000-0000-000000000000/url',
    'http://localhost:5204/api/attachments/00000000-0000-0000-0000-000000000000/url'
)
foreach ($u in $urls) {
    try {
        $r = Invoke-WebRequest -Uri $u -Method Get -UseBasicParsing -ErrorAction Stop
        "OK $($r.StatusCode) $u"
    } catch {
        $code = $null
        if ($_.Exception.Response) { $code = $_.Exception.Response.StatusCode.value__ }
        "ERR $code $u"
    }
}
