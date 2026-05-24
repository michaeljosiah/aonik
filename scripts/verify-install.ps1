# scripts/verify-install.ps1
#
# End-to-end smoke test for a fresh Aonik install. Asserts every surface that
# Run with Aspire + Bootstrap your first tenant should have produced. Exits
# with code 0 on full pass, 1 on any failure — suitable for CI / agents.
#
# Usage:
#   .\scripts\verify-install.ps1
#   .\scripts\verify-install.ps1 -OwnerEmail admin@aonik.local
#   .\scripts\verify-install.ps1 -ApiUrl https://localhost:5001 -OwnerEmail you@example.com
#
# The Keycloak realm assertion only runs when the environment variable
# AONIK_AUTH_PROVIDER equals "Keycloak" (case-insensitive). The rest of
# the assertions run unconditionally.
#
# Mirrors the inline script documented at
#   apps/docs-site/content/docs/operate/install-configure/verify.mdx
# so the docs and the repo stay in lockstep.

#requires -Version 7
param(
  [string]$ApiUrl      = 'https://localhost:5001',
  [string]$AdminUrl    = 'http://localhost:5173',
  [string]$QdrantUrl   = 'http://localhost:6333',
  [string]$KeycloakUrl = 'http://localhost:8080',
  [string]$OwnerEmail  = 'admin@aonik.local'
)

$ErrorActionPreference = 'Stop'
$failures = @()

function Assert($name, $scriptblock) {
  try {
    & $scriptblock
    Write-Host "[OK] $name" -ForegroundColor Green
  }
  catch {
    Write-Host "[FAIL] $name - $_" -ForegroundColor Red
    $script:failures += $name
  }
}

# 1. API process is reachable.
Assert 'API /health is Healthy' {
  $h = curl.exe --insecure --silent --max-time 5 "$ApiUrl/health" | ConvertFrom-Json
  if ($h.Status -ne 'Healthy') { throw "Status: $($h.Status)" }
}

# 2. Scalar API reference is served.
Assert 'API /scalar serves an HTML page' {
  $code = curl.exe --insecure --silent --output $null --write-out '%{http_code}' "$ApiUrl/scalar"
  if ($code -ne '200') { throw "HTTP $code" }
}

# 3. Bootstrap is completed.
Assert 'Bootstrap status is completed' {
  $s = curl.exe --insecure --silent --max-time 5 "$ApiUrl/bootstrap/status" | ConvertFrom-Json
  if ($s.state -ne 'completed') { throw "state: $($s.state)" }
}

# 4. Exactly one tenant exists.
Assert 'Exactly one tenant exists' {
  $count = sqlcmd -S '(localdb)\MSSQLLocalDB' -d AonikDb -h -1 -Q 'SET NOCOUNT ON; SELECT COUNT(*) FROM AnkTenants' |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -match '^\d+$' } |
    Select-Object -First 1
  if ([int]$count -ne 1) { throw "tenant count: $count" }
}

# 5. Owner user exists with both admin roles.
Assert 'Owner user exists with PlatformAdmin role' {
  $rows = sqlcmd -S '(localdb)\MSSQLLocalDB' -d AonikDb -h -1 -Q @"
SET NOCOUNT ON;
SELECT r.Name
FROM AnkUserRoles ur
JOIN AnkUsers u ON u.Id = ur.UserId
JOIN AnkRoles r ON r.Id = ur.RoleId
WHERE u.Email = '$OwnerEmail';
"@
  $rolesFound = $rows |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -in 'PlatformAdmin', 'TenantAdmin' } |
    Sort-Object -Unique
  if ($rolesFound.Count -lt 2) { throw "roles found: $($rolesFound -join ',')" }
}

# 6. Admin UI dev server is reachable.
Assert 'Admin UI returns 200' {
  $code = curl.exe --silent --output $null --write-out '%{http_code}' "$AdminUrl/"
  if ($code -ne '200') { throw "HTTP $code" }
}

# 7. Qdrant readiness probe.
Assert 'Qdrant is ready' {
  $body = curl.exe --silent --max-time 5 "$QdrantUrl/readyz"
  if ($body -notmatch 'all shards are ready') { throw "readyz: $body" }
}

# 8. Qdrant container is healthy.
Assert 'Qdrant container is healthy' {
  $row = docker ps --filter 'ancestor=qdrant/qdrant' --format '{{.Status}}' | Select-Object -First 1
  if (-not $row -or $row -notmatch 'Up ') { throw "container status: $row" }
}

# 9. Public OpenAPI document is served.
Assert 'OpenAPI document is served' {
  $body = curl.exe --insecure --silent --max-time 5 "$ApiUrl/openapi/v1.json"
  if ($body -notmatch '"openapi"') { throw 'no openapi field in response' }
}

# 10. (Keycloak only) Realm discovery document is served.
if ($env:AONIK_AUTH_PROVIDER -ieq 'Keycloak') {
  Assert 'Keycloak realm OIDC discovery is served' {
    $body = curl.exe --silent --max-time 5 "$KeycloakUrl/realms/aonik/.well-known/openid-configuration"
    if ($body -notmatch '"issuer"\s*:\s*"http://localhost:8080/realms/aonik"') {
      throw 'discovery document missing or wrong issuer'
    }
  }
}

if ($failures.Count -gt 0) {
  Write-Host ''
  Write-Host "Verification FAILED. Failures: $($failures -join ', ')" -ForegroundColor Red
  exit 1
}

Write-Host ''
Write-Host 'Install verified. The Aonik platform is healthy and a tenant is in place.' -ForegroundColor Green
if ($env:AONIK_AUTH_PROVIDER -ieq 'Keycloak') {
  Write-Host "Sign in at $AdminUrl as $OwnerEmail / Aonik!Dev2026" -ForegroundColor Cyan
}
else {
  Write-Host "Next step: wire identity in /docs/operate/identity-access so the owner can sign in." -ForegroundColor Cyan
}
exit 0
