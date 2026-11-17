$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoDir = (Resolve-Path (Join-Path $scriptDir "../..")).Path
$project = Join-Path $repoDir "backend/src/Api/Api.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK is required but 'dotnet' was not found."
}

$secretLines = @()
try {
    $secretLines = & dotnet user-secrets list --project $project 2>$null
} catch {
    $secretLines = @()
}

function Get-ExistingSecret([string]$key) {
    $prefix = "$key = "
    $line = $secretLines |
        Where-Object { $_.StartsWith($prefix, [System.StringComparison]::Ordinal) } |
        Select-Object -First 1
    if ($null -ne $line) {
        return $line.Substring($prefix.Length)
    }
    return $null
}

function New-RandomHex {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $bytes = New-Object byte[] 32
        $rng.GetBytes($bytes)
    } finally {
        $rng.Dispose()
    }
    return -join ($bytes | ForEach-Object { $_.ToString("x2") })
}

$connectionString = $env:DB_CONNECTION
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString = Get-ExistingSecret "ConnectionStrings:Database"
}
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString = "Host=localhost;Port=5432;Database=payment_reconciliation;Username=postgres;Password=postgres"
}

$jwtSigningKey = $env:JWT_SIGNING_KEY
if ([string]::IsNullOrWhiteSpace($jwtSigningKey)) {
    $jwtSigningKey = Get-ExistingSecret "Jwt:SigningKey"
}
if ([string]::IsNullOrWhiteSpace($jwtSigningKey)) {
    $jwtSigningKey = New-RandomHex
}

$webhookSecret = $env:WEBHOOK_SIGNING_SECRET
if ([string]::IsNullOrWhiteSpace($webhookSecret)) {
    $webhookSecret = Get-ExistingSecret "Webhooks:SigningSecret"
}
if ([string]::IsNullOrWhiteSpace($webhookSecret)) {
    $webhookSecret = "whsec_$(New-RandomHex)"
}

$mockSecret = $env:WEBHOOK_MOCK_SECRET
if ([string]::IsNullOrWhiteSpace($mockSecret)) {
    $mockSecret = Get-ExistingSecret "Webhooks:Providers:mock:SigningSecret"
}
if ([string]::IsNullOrWhiteSpace($mockSecret)) {
    $mockSecret = "whsec_$(New-RandomHex)"
}

$stripeSecret = $env:WEBHOOK_STRIPE_SECRET
if ([string]::IsNullOrWhiteSpace($stripeSecret)) {
    $stripeSecret = Get-ExistingSecret "Webhooks:Providers:stripe:SigningSecret"
}
if ([string]::IsNullOrWhiteSpace($stripeSecret)) {
    $stripeSecret = "whsec_$(New-RandomHex)"
}

$demoPassword = $env:DEMO_PASSWORD
if ([string]::IsNullOrWhiteSpace($demoPassword)) {
    $demoPassword = Get-ExistingSecret "Demo:Password"
}
if ([string]::IsNullOrWhiteSpace($demoPassword)) {
    $demoPassword = "Demo123!ChangeMe"
}

$platformPassword = $env:PLATFORM_PASSWORD
if ([string]::IsNullOrWhiteSpace($platformPassword)) {
    $platformPassword = Get-ExistingSecret "Demo:PlatformPassword"
}
if ([string]::IsNullOrWhiteSpace($platformPassword)) {
    $platformPassword = "Platform123!ChangeMe"
}

& dotnet user-secrets set "ConnectionStrings:Database" $connectionString --project $project | Out-Null
& dotnet user-secrets set "Jwt:SigningKey" $jwtSigningKey --project $project | Out-Null
& dotnet user-secrets set "Webhooks:SigningSecret" $webhookSecret --project $project | Out-Null
& dotnet user-secrets set "Webhooks:Providers:mock:SigningSecret" $mockSecret --project $project | Out-Null
& dotnet user-secrets set "Webhooks:Providers:stripe:SigningSecret" $stripeSecret --project $project | Out-Null
& dotnet user-secrets set "Demo:Password" $demoPassword --project $project | Out-Null
& dotnet user-secrets set "Demo:PlatformPassword" $platformPassword --project $project | Out-Null

Write-Host "Local user-secrets configured for backend/src/Api."
Write-Host ""
Write-Host "Demo owner:     avery.chen@ledgerline.local / $demoPassword"
Write-Host "Platform owner: platform@ledgerline.local / $platformPassword"
Write-Host "Tenant slug:    demo"
Write-Host ""
Write-Host "Inspect local secrets with:"
Write-Host "  dotnet user-secrets list --project backend/src/Api"
