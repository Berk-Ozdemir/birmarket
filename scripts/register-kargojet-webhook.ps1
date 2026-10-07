$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot 'birmarket.env'
if (-not (Test-Path -LiteralPath $environmentFile -PathType Leaf)) {
    throw 'Create the local birmarket.env file before registering the webhook.'
}

$settings = @{}
foreach ($line in [IO.File]::ReadLines($environmentFile)) {
    $trimmed = $line.Trim()
    if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#') -or -not $trimmed.Contains('=')) { continue }
    $separator = $trimmed.IndexOf('=')
    $settings[$trimmed.Substring(0, $separator).Trim()] = $trimmed.Substring($separator + 1).Trim().Trim('"').Trim("'")
}

$apiToken = $settings['kargojet_api_token']
$publicBaseUrl = $settings['birmarket_public_base_url']
$parsedBaseUrl = $null
if ([string]::IsNullOrWhiteSpace($apiToken)) { throw 'Set kargojet_api_token in birmarket.env first.' }
if (-not [Uri]::TryCreate($publicBaseUrl, [UriKind]::Absolute, [ref]$parsedBaseUrl) -or $parsedBaseUrl.Scheme -ne [Uri]::UriSchemeHttps) {
    throw 'Set birmarket_public_base_url to the public HTTPS storefront URL first.'
}
if (-not [string]::IsNullOrWhiteSpace($settings['kargojet_webhook_secret'])) {
    throw 'A KargoJet webhook secret is already configured. Do not register another endpoint without reviewing the provider account.'
}

$body = @{
    url = "$($publicBaseUrl.TrimEnd('/'))/api/webhooks/kargojet"
    events = @('shipment.created', 'shipment.in_transit', 'shipment.delivered', 'shipment.returned', 'tracking.updated')
} | ConvertTo-Json -Depth 4

try {
    $response = Invoke-RestMethod -Method Post `
        -Uri 'https://api.kargojet.com/partner-api/v1/webhooks/endpoints' `
        -Headers @{ Authorization = "Bearer $apiToken"; 'Idempotency-Key' = 'birmarket-kargojet-webhook-v1' } `
        -ContentType 'application/json' `
        -Body $body
} catch {
    throw 'KargoJet webhook registration failed. Check the token, account, and public HTTPS endpoint, then review the provider panel before retrying.'
}

$secret = [string]$response.secret
if ([string]::IsNullOrWhiteSpace($secret)) {
    throw 'KargoJet did not return a webhook secret. Review the provider panel before retrying.'
}

$contents = [IO.File]::ReadAllText($environmentFile)
$pattern = '(?m)^kargojet_webhook_secret=.*$'
if ([regex]::IsMatch($contents, $pattern)) {
    $contents = [regex]::Replace($contents, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($match) "kargojet_webhook_secret=$secret" })
} else {
    $contents = $contents.TrimEnd() + "`n" + "kargojet_webhook_secret=$secret`n"
}
[IO.File]::WriteAllText($environmentFile, $contents, [Text.UTF8Encoding]::new($false))
Write-Host "Registered KargoJet webhook $($response.id). The returned secret was stored in the ignored local birmarket.env file."
