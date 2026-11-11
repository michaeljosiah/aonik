param(
    [switch]$CheckAbandonedJob,
    [ValidateSet('standard', 'large-v1')][string]$CapacityProfile = 'standard',
    [string]$ExpectedImage
)

$ErrorActionPreference = 'Stop'
$subscription = '336d38e2-bb96-4510-a4c5-5c313139eef9'
$large = $CapacityProfile -eq 'large-v1'
$poolName = if ($large) { 'aonikdevpublishinglarge' } else { 'aonikdevpublishing' }
$poolId = "/subscriptions/$subscription/resourceGroups/rg-aonik-dev-uksouth/providers/Microsoft.App/sessionPools/$poolName"
$apiVersion = '2025-02-02-preview'
$profile = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'iac/azure/environments/dev/aca.parameters.json') -Raw | ConvertFrom-Json
if (!$ExpectedImage) {
    if ($large) { throw 'The large profile requires its explicitly qualified immutable image.' }
    $ExpectedImage = $profile.parameters.publishingAppSettings.value.'Publishing__Verifier__Image'
}
if ($ExpectedImage -notmatch '^aonikdevacr[.]azurecr[.]io/aonik-arke-verifier-sessions@sha256:[a-f0-9]{64}$') { throw 'An immutable dev registry image is required.' }
if ((az account show --query id -o tsv) -ne $subscription) { throw 'Select the dev subscription.' }
$pool = az rest --method get --url "https://management.azure.com${poolId}?api-version=$apiVersion" -o json | ConvertFrom-Json
$endpoint = $pool.properties.poolManagementEndpoint.TrimEnd('/')
if ($LASTEXITCODE -ne 0 -or $pool.properties.provisioningState -ne 'Succeeded' -or $endpoint -notmatch "^https://$poolName[.][a-z0-9.-]+[.]azurecontainerapps[.]io`$") {
    throw 'The dedicated dev verifier pool is not ready.'
}
if ($pool.properties.customContainerTemplate.containers[0].image -cne $expectedImage -or
    $pool.properties.sessionNetworkConfiguration.status -ne 'EgressDisabled' -or
    $pool.properties.managedIdentitySettings[0].lifecycle -ne 'None') { throw 'The dev verifier policy differs from the qualified profile.' }
$token = az account get-access-token --resource https://dynamicsessions.io --query accessToken -o tsv
if ($LASTEXITCODE -ne 0 -or !$token) { throw 'Cannot authenticate to the dev session pool.' }
$session = 'qualification-' + [Convert]::ToHexStringLower([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$headers = @{Authorization="Bearer $token"}
try {
    $probe = Invoke-RestMethod -Uri "$endpoint/qualify?identifier=$session&api-version=$apiVersion" -Method Post -Headers $headers -Body '' -TimeoutSec 60
    if ($probe.uid -ne 65532 -or $probe.writableRuntime -or $probe.scratchBytes -ne 67108864 -or
        $probe.network.public -ne 'denied' -or $probe.network.metadata -ne 'denied' -or
        $probe.childLimits.addressSpaceBytes -ne 2147483648 -or !$probe.childLimits.oversizedAllocationDenied -or
        $probe.childLimits.cpuSeconds -ne $(if ($large) { 180 } else { 45 }) -or $probe.childLimits.processes -ne 32 -or
        $probe.childLimits.fileBytes -ne $(if ($large) { 67108864 } else { 16777216 }) -or
        $pool.properties.customContainerTemplate.containers[0].resources.cpu -ne $(if ($large) { 2 } else { 1 }) -or
        $pool.properties.customContainerTemplate.containers[0].resources.memory -ne $(if ($large) { '4Gi' } else { '2Gi' }) -or
        ($large -and ($probe.capacityProfile -ne 'large-v1' -or $probe.maxPackageBytes -ne 1610612736 -or $probe.maxAssets -ne 2048 -or $probe.maxReportBytes -ne 1048576))) {
        $probe | ConvertTo-Json -Depth 6
        throw 'Runtime isolation qualification failed.'
    }
    [pscustomobject]@{utc=[DateTime]::UtcNow.ToString('o'); image=$pool.properties.customContainerTemplate.containers[0].image; runtime=$probe} | ConvertTo-Json -Depth 6
    if ($CheckAbandonedJob) {
        $nonce = [Convert]::ToHexStringLower([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        $body = @{nonce=$nonce;sha256=('a'*64);bytes=1;maxAssets=1;maxAssetBytes=1;maxDocumentBytes=1;timeoutSeconds=1} | ConvertTo-Json
        $null = Invoke-RestMethod -Uri "$endpoint/begin?identifier=$session&api-version=$apiVersion" -Method Post -Headers $headers -ContentType 'application/json' -Body $body -TimeoutSec 30
        # Deliberately abandon the job. Never refresh its job deadline or manually stop it before observing expiry.
        $started = [DateTime]::UtcNow
        do {
            Start-Sleep -Seconds 10
            $elapsed = [int]([DateTime]::UtcNow - $started).TotalSeconds
            if ($elapsed -lt 160) { continue }
            $response = Invoke-WebRequest -Uri "$endpoint/.management/getSession?identifier=$session&api-version=$apiVersion" -Method Post -Headers $headers -Body '' -SkipHttpErrorCheck -TimeoutSec 15
            if ([int]$response.StatusCode -eq 400 -and $response.Content.Contains('SessionWithIdentifierNotFound')) {
                [pscustomobject]@{abandonedJobReclaimed=$true;observedAfterSeconds=$elapsed} | ConvertTo-Json
                break
            }
            if ($elapsed -ge 330) { throw 'Abandoned session was not reclaimed within its hard lifetime.' }
        } while ($true)
    }
} finally {
    $null = Invoke-WebRequest -Uri "$endpoint/.management/stopSession?identifier=$session&api-version=$apiVersion" -Method Post -Headers $headers -Body '' -SkipHttpErrorCheck -TimeoutSec 15
    $headers.Clear()
    $token = $null
}
