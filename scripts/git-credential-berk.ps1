param(
    [Parameter(Position = 0)]
    [string]$Operation
)

$request = [Console]::In.ReadToEnd()
if ($Operation -ne 'get' -or $request -notmatch '(?m)^host=github\.com$') {
    exit 0
}

$credential = & gh auth token --hostname github.com --user berk-ozdemir
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($credential)) {
    exit 1
}

[Console]::Out.WriteLine('username=x-access-token')
[Console]::Out.WriteLine("password=$credential")
[Console]::Out.WriteLine()
$credential = $null
