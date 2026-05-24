# scripts/onboard.ps1
#
# End-to-end onboarding for a fresh contributor — clone the repo, run this
# script, sign in. The script automates everything the docs at
# /docs/operate/install-configure walk through:
#
#   1. Verify prerequisites (.NET 10, Docker, LocalDB, Node, sqlcmd, curl)
#   2. Configure user-secrets for /bootstrap (Bootstrap:Enabled +
#      Bootstrap:SetupSecret)
#   3. Optionally enable Keycloak (default — zero external accounts) by
#      setting AONIK_AUTH_PROVIDER=Keycloak for the AppHost process
#   4. Launch the Aspire orchestrator in a new PowerShell window
#   5. Wait for API /health + (in Keycloak mode) realm discovery
#   6. POST /bootstrap with the right owner email
#   7. Run scripts/verify-install.ps1 to assert end-to-end health
#   8. Print sign-in URL + credentials
#
# Re-running is safe: every step is idempotent. Existing bootstrap secrets
# are re-used. An already-bootstrapped tenant short-circuits step 6.
#
# Usage:
#   pwsh ./scripts/onboard.ps1                  # default: Keycloak path
#   pwsh ./scripts/onboard.ps1 -Provider Auth0  # skip the Keycloak opt-in
#   pwsh ./scripts/onboard.ps1 -OwnerEmail you@example.com
#   pwsh ./scripts/onboard.ps1 -SkipPrereqs     # skip the tool-version checks
#   pwsh ./scripts/onboard.ps1 -NoLaunch        # don't auto-start the AppHost
#   pwsh ./scripts/onboard.ps1 -Reset           # drop AonikDb and start over

#requires -Version 7
[CmdletBinding()]
param(
  [ValidateSet('Keycloak', 'Auth0', 'AzureAd', 'None')]
  [string]$Provider = 'Keycloak',

  [string]$OwnerEmail,

  [string]$ApiUrl = 'https://localhost:5001',

  [int]$HealthTimeoutSeconds = 180,

  [switch]$SkipPrereqs,

  [switch]$NoLaunch,

  [switch]$Reset
)

$ErrorActionPreference = 'Stop'

# ─────────────────────────────────────────────────────────────────────────
# Locate repo root.
#
# This script lives in scripts/, so the repo root is one level up. Anchoring
# every path on $RepoRoot means the script works from any cwd.
# ─────────────────────────────────────────────────────────────────────────
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$ApiProject = Join-Path $RepoRoot 'src/Aonik.Api'
$AppHostProject = Join-Path $RepoRoot 'src/Aonik.AppHost'
$VerifyScript = Join-Path $PSScriptRoot 'verify-install.ps1'

# Default owner email picks the pre-seeded Keycloak admin when on the
# Keycloak path; falls back to a placeholder for the other providers so
# the script still runs (the user can override with -OwnerEmail).
if (-not $OwnerEmail) {
  $OwnerEmail = if ($Provider -eq 'Keycloak') { 'admin@aonik.local' } else { 'you@example.com' }
}

function Write-Banner($text) {
  Write-Host ''
  Write-Host ('─' * 72) -ForegroundColor DarkGray
  Write-Host $text -ForegroundColor Cyan
  Write-Host ('─' * 72) -ForegroundColor DarkGray
}

function Test-Tool($name, $cmd, $pattern, $help) {
  try {
    $out = Invoke-Expression $cmd 2>&1 | Out-String
    if ($out -match $pattern) {
      Write-Host "  [OK]   $name" -ForegroundColor Green
      return $true
    }
    Write-Host "  [FAIL] $name (no match for '$pattern')" -ForegroundColor Red
    if ($help) { Write-Host "         $help" -ForegroundColor DarkYellow }
    return $false
  }
  catch {
    Write-Host "  [FAIL] $name ($_)" -ForegroundColor Red
    if ($help) { Write-Host "         $help" -ForegroundColor DarkYellow }
    return $false
  }
}

# ─────────────────────────────────────────────────────────────────────────
# Step 1: Prerequisites.
#
# Mirrors the deterministic check block in
# apps/docs-site/content/docs/operate/install-configure/prerequisites.mdx.
# Any failure here exits early — there's no point trying to bootstrap if
# Docker isn't running or LocalDB isn't installed.
# ─────────────────────────────────────────────────────────────────────────
function Invoke-PrereqsCheck {
  Write-Banner 'Step 1 / 6 - Verifying prerequisites'
  $failures = @()

  if (-not (Test-Tool 'PowerShell 7+' "`$PSVersionTable.PSVersion.Major" '^[7-9]|^[1-9][0-9]+' 'Install PowerShell 7: winget install --id Microsoft.PowerShell')) { $failures += 'pwsh' }
  if (-not (Test-Tool 'Git 2.40+' 'git --version' '2\.([4-9][0-9]|[5-9][0-9]|[1-9][0-9]{2})' 'Install Git: winget install --id Git.Git')) { $failures += 'git' }
  if (-not (Test-Tool '.NET 10 SDK' 'dotnet --list-sdks' '^10\.' 'Install .NET 10: winget install --id Microsoft.DotNet.SDK.10')) { $failures += 'dotnet' }
  if (-not (Test-Tool 'Docker daemon' "docker info --format '{{.ServerVersion}}'" '^\d+\.\d+\.\d+' 'Start Docker Desktop and wait for the tray icon to report running')) { $failures += 'docker' }
  if (-not (Test-Tool 'LocalDB running' 'sqllocaldb info MSSQLLocalDB' 'State:\s*Running' 'Run: sqllocaldb start MSSQLLocalDB')) { $failures += 'localdb' }
  if (-not (Test-Tool 'Node 18.18+' 'node --version' '^v(1[89]\.|2[0-9]\.|[3-9][0-9])' 'Install Node 18+: winget install --id OpenJS.NodeJS.LTS')) { $failures += 'node' }
  if (-not (Test-Tool 'curl.exe available' 'curl.exe --version' '^curl\s+\d' 'curl.exe ships with Windows 10+; reinstall PowerShell or restore curl in System32 if missing')) { $failures += 'curl' }
  if (-not (Test-Tool 'sqlcmd available' 'sqlcmd -?' 'Microsoft' 'Install sqlcmd: winget install --id Microsoft.Sqlcmd')) { $failures += 'sqlcmd' }

  if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host "Prerequisites failed: $($failures -join ', ')" -ForegroundColor Red
    Write-Host 'Fix the failing tools (see https://docs.aonik.dev/operate/install-configure/prerequisites) and re-run this script.' -ForegroundColor Yellow
    exit 1
  }
}

# ─────────────────────────────────────────────────────────────────────────
# Step 2: Configure /bootstrap via user-secrets.
#
# Idempotent: only generates a new secret when one isn't already set.
# The setup secret is a one-time gate on the anonymous /bootstrap endpoint.
# ─────────────────────────────────────────────────────────────────────────
function Initialize-BootstrapSecret {
  Write-Banner 'Step 2 / 6 - Configuring user-secrets for /bootstrap'

  $listing = dotnet user-secrets --project $ApiProject list 2>&1 | Out-String

  $enabledLine = $listing -split "`n" | Where-Object { $_ -match '^Bootstrap:Enabled\s*=\s*' } | Select-Object -First 1
  if (-not $enabledLine -or $enabledLine -notmatch '=\s*true') {
    dotnet user-secrets --project $ApiProject set Bootstrap:Enabled true | Out-Null
    Write-Host '  [OK]   Bootstrap:Enabled set to true' -ForegroundColor Green
  }
  else {
    Write-Host '  [OK]   Bootstrap:Enabled is already true' -ForegroundColor Green
  }

  $secretLine = $listing -split "`n" | Where-Object { $_ -match '^Bootstrap:SetupSecret\s*=\s*' } | Select-Object -First 1
  $secret = if ($secretLine) { ($secretLine -split ' = ', 2)[1].Trim() } else { '' }

  if (-not $secret) {
    $secret = [guid]::NewGuid().ToString()
    dotnet user-secrets --project $ApiProject set Bootstrap:SetupSecret $secret | Out-Null
    Write-Host '  [OK]   Bootstrap:SetupSecret generated and stored' -ForegroundColor Green
  }
  else {
    Write-Host '  [OK]   Bootstrap:SetupSecret already exists; reusing it' -ForegroundColor Green
  }

  $script:BootstrapSecret = $secret
}

# ─────────────────────────────────────────────────────────────────────────
# Step 3: Pick the auth provider.
#
# For Keycloak (the default), we just set the env var that AppHost.cs
# reads. AppHost auto-wires every other env var (Auth__Provider,
# Auth__Keycloak__Authority, VITE_AUTH_PROVIDER, ...) so the developer
# never has to touch appsettings or .env files.
# ─────────────────────────────────────────────────────────────────────────
function Set-AuthProvider {
  Write-Banner "Step 3 / 6 - Selecting auth provider: $Provider"

  if ($Provider -eq 'Keycloak') {
    $env:AONIK_AUTH_PROVIDER = 'Keycloak'
    Write-Host '  [OK]   AONIK_AUTH_PROVIDER=Keycloak set for this session' -ForegroundColor Green
    Write-Host '         The AppHost spins up a Keycloak 26 container on http://localhost:8080' -ForegroundColor DarkGray
    Write-Host '         with the pre-seeded aonik realm and the admin@aonik.local user.' -ForegroundColor DarkGray
  }
  else {
    # Clear any leftover Keycloak env var from a previous run so the
    # AppHost doesn't pick it up accidentally.
    $env:AONIK_AUTH_PROVIDER = ''
    Write-Host "  [OK]   Provider is $Provider. You will need to configure it under" -ForegroundColor Green
    Write-Host '         /docs/operate/identity-access before sign-in works.' -ForegroundColor DarkGray
  }
}

# ─────────────────────────────────────────────────────────────────────────
# Optional Step: -Reset.
#
# Drops the LocalDB AonikDb so the next bootstrap starts from scratch.
# Pure dev convenience; refuses to run if pointed at anything that isn't
# the local LocalDB instance.
# ─────────────────────────────────────────────────────────────────────────
function Invoke-DevReset {
  Write-Banner 'Optional - Resetting AonikDb (dev only)'
  sqlcmd -S '(localdb)\MSSQLLocalDB' -Q 'DROP DATABASE IF EXISTS AonikDb' -h -1 | Out-Null
  Write-Host '  [OK]   Dropped AonikDb on (localdb)\MSSQLLocalDB' -ForegroundColor Green
  Write-Host '         The AppHost will re-create + migrate it on next start.' -ForegroundColor DarkGray
}

# ─────────────────────────────────────────────────────────────────────────
# Step 4: Launch the AppHost.
#
# Start in a new PowerShell window so the long-running orchestrator can
# emit live logs while this script keeps progressing toward bootstrap.
# Skippable via -NoLaunch for users who prefer to run the AppHost
# themselves (e.g. inside an IDE debugger).
# ─────────────────────────────────────────────────────────────────────────
function Start-AppHost {
  Write-Banner 'Step 4 / 6 - Starting the Aspire AppHost'

  if ($NoLaunch) {
    Write-Host '  -NoLaunch was passed.' -ForegroundColor Yellow
    Write-Host "  Open a second terminal and run:" -ForegroundColor Yellow
    Write-Host "    `$env:AONIK_AUTH_PROVIDER = '$Provider'" -ForegroundColor White
    Write-Host '    dotnet run --project src/Aonik.AppHost' -ForegroundColor White
    Write-Host '  This script will wait below for /health to respond.' -ForegroundColor Yellow
    return
  }

  $launchCommand = "Set-Location '$RepoRoot'; `$env:AONIK_AUTH_PROVIDER='$($env:AONIK_AUTH_PROVIDER)'; dotnet run --project '$AppHostProject'"
  Start-Process pwsh -ArgumentList @('-NoExit', '-NoProfile', '-Command', $launchCommand)
  Write-Host '  [OK]   AppHost launched in a new PowerShell window' -ForegroundColor Green
  Write-Host '         Aspire dashboard: https://localhost:21183' -ForegroundColor DarkGray
}

# ─────────────────────────────────────────────────────────────────────────
# Step 5: Wait for the API to come up.
#
# Polls /health until the response is Healthy or the timeout fires.
# First-run image pulls (Qdrant, Keycloak) can take a minute; the default
# 180-second budget covers most laptops.
# ─────────────────────────────────────────────────────────────────────────
function Wait-ForApi {
  Write-Banner "Step 5 / 6 - Waiting for the API to report Healthy (timeout: ${HealthTimeoutSeconds}s)"

  $deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
  $lastError = $null

  while ((Get-Date) -lt $deadline) {
    try {
      $body = curl.exe --insecure --silent --max-time 5 "$ApiUrl/health" 2>$null
      if ($body) {
        $h = $body | ConvertFrom-Json -ErrorAction Stop
        if ($h.Status -eq 'Healthy') {
          Write-Host '  [OK]   API /health is Healthy' -ForegroundColor Green
          return
        }
        $lastError = "Status: $($h.Status)"
      }
    }
    catch {
      $lastError = $_.Exception.Message
    }
    Start-Sleep -Seconds 3
    Write-Host '         still waiting...' -ForegroundColor DarkGray
  }

  Write-Host "  [FAIL] API did not become Healthy within ${HealthTimeoutSeconds}s. Last error: $lastError" -ForegroundColor Red
  Write-Host '         Check the AppHost console window and the Aspire dashboard for failed resources.' -ForegroundColor Yellow
  exit 1
}

function Wait-ForKeycloak {
  if ($Provider -ne 'Keycloak') { return }

  Write-Host '  Waiting for Keycloak realm discovery...' -ForegroundColor DarkGray
  $deadline = (Get-Date).AddSeconds(120)
  while ((Get-Date) -lt $deadline) {
    $code = curl.exe --silent --output $null --write-out '%{http_code}' --max-time 3 'http://localhost:8080/realms/aonik/.well-known/openid-configuration' 2>$null
    if ($code -eq '200') {
      Write-Host '  [OK]   Keycloak realm is serving /.well-known/openid-configuration' -ForegroundColor Green
      return
    }
    Start-Sleep -Seconds 3
    Write-Host '         still waiting for Keycloak...' -ForegroundColor DarkGray
  }
  Write-Host '  [WARN] Keycloak did not respond within 120s. The bootstrap step will still run,' -ForegroundColor Yellow
  Write-Host '         but sign-in will fail until the keycloak resource recovers in the Aspire dashboard.' -ForegroundColor Yellow
}

# ─────────────────────────────────────────────────────────────────────────
# Step 6: Bootstrap.
#
# Idempotent: a 409 from /bootstrap means a tenant already exists. We
# treat that as success — the next step (verify) will confirm the existing
# state matches what we want.
# ─────────────────────────────────────────────────────────────────────────
function Invoke-Bootstrap {
  Write-Banner 'Step 6 / 6 - Bootstrapping the first tenant'

  $status = curl.exe --insecure --silent --max-time 5 "$ApiUrl/bootstrap/status" | ConvertFrom-Json
  if ($status.state -eq 'completed') {
    Write-Host '  [OK]   Bootstrap already completed; skipping' -ForegroundColor Green
    return
  }
  if ($status.state -ne 'ready') {
    Write-Host "  [FAIL] /bootstrap/status reports state=$($status.state); $($status.message)" -ForegroundColor Red
    Write-Host '         See /docs/operate/install-configure/bootstrap-tenant for the recovery flow.' -ForegroundColor Yellow
    exit 1
  }

  $payload = @{
    setupSecret      = $script:BootstrapSecret
    ownerEmail       = $OwnerEmail
    ownerDisplayName = 'Platform Admin'
  } | ConvertTo-Json -Compress

  $response = curl.exe --insecure --silent --show-error `
    -X POST "$ApiUrl/bootstrap" `
    -H 'Content-Type: application/json' `
    -d $payload 2>&1

  try {
    $parsed = $response | ConvertFrom-Json -ErrorAction Stop
  }
  catch {
    Write-Host "  [FAIL] Bootstrap response was not JSON: $response" -ForegroundColor Red
    exit 1
  }

  if ($parsed.success -ne $true) {
    Write-Host "  [FAIL] Bootstrap failed: $($parsed | ConvertTo-Json -Compress)" -ForegroundColor Red
    exit 1
  }

  Write-Host "  [OK]   Tenant created: $($parsed.tenantName) ($($parsed.tenantId))" -ForegroundColor Green
  Write-Host "  [OK]   Owner user:    $($parsed.ownerEmail) ($($parsed.userId))" -ForegroundColor Green
}

# ─────────────────────────────────────────────────────────────────────────
# Wrap-up: run the verify script + print sign-in instructions.
# ─────────────────────────────────────────────────────────────────────────
function Show-SuccessBanner {
  Write-Banner 'Onboarding complete'
  Write-Host ''

  if ($Provider -eq 'Keycloak') {
    Write-Host '  Sign in:' -ForegroundColor Cyan
    Write-Host '    URL:      http://localhost:5173' -ForegroundColor White
    Write-Host "    Email:    $OwnerEmail" -ForegroundColor White
    Write-Host '    Password: Aonik!Dev2026' -ForegroundColor White
    Write-Host ''
    Write-Host '  Aspire dashboard:    https://localhost:21183' -ForegroundColor DarkGray
    Write-Host '  API Scalar:          https://localhost:5001/scalar' -ForegroundColor DarkGray
    Write-Host '  Keycloak admin UI:   http://localhost:8080/admin/ (admin / admin)' -ForegroundColor DarkGray
  }
  else {
    Write-Host '  Aonik backend is up, but the SPA cannot sign in until you wire' -ForegroundColor Yellow
    Write-Host "  $Provider per /docs/operate/identity-access." -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  Aspire dashboard:    https://localhost:21183' -ForegroundColor DarkGray
    Write-Host '  API Scalar:          https://localhost:5001/scalar' -ForegroundColor DarkGray
  }
  Write-Host ''
  Write-Host '  Leave the AppHost window open for as long as you want the stack running.' -ForegroundColor DarkGray
  Write-Host '  Ctrl+C in that window stops every resource cleanly.' -ForegroundColor DarkGray
  Write-Host ''
}

# ─────────────────────────────────────────────────────────────────────────
# Entry point.
# ─────────────────────────────────────────────────────────────────────────

Write-Host ''
Write-Host '   Aonik onboarding   ' -ForegroundColor White -BackgroundColor DarkCyan
Write-Host "   Provider: $Provider | Owner: $OwnerEmail   " -ForegroundColor DarkGray
Write-Host ''

if (-not $SkipPrereqs) { Invoke-PrereqsCheck }
Initialize-BootstrapSecret
Set-AuthProvider
if ($Reset) { Invoke-DevReset }
Start-AppHost
Wait-ForApi
Wait-ForKeycloak
Invoke-Bootstrap

# Run verify-install.ps1 as the final assertion. Failures bubble up as
# exit 1; success prints its own green summary line.
Write-Banner 'Running scripts/verify-install.ps1 for end-to-end verification'
& $VerifyScript -ApiUrl $ApiUrl -OwnerEmail $OwnerEmail
if ($LASTEXITCODE -ne 0) {
  Write-Host ''
  Write-Host 'Verification failed — see the failures listed above and the Aspire dashboard.' -ForegroundColor Red
  exit 1
}

Show-SuccessBanner
exit 0
