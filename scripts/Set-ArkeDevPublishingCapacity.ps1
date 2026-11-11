param([ValidateSet('standard','large-v1')][string]$Profile = 'large-v1', [switch]$Apply)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$subscription = '336d38e2-bb96-4510-a4c5-5c313139eef9'
if ((az account show --query id -o tsv) -ne $subscription) { throw 'Select the dev subscription.' }
$settings = Get-Content (Join-Path $root 'iac/azure/environments/dev/publishing-capacity-large.json') -Raw | ConvertFrom-Json -AsHashtable
if ($Profile -eq 'standard') {
    $base = (Get-Content (Join-Path $root 'iac/azure/environments/dev/aca.parameters.json') -Raw | ConvertFrom-Json -AsHashtable).parameters.publishingAppSettings.value
    foreach ($key in @($settings.Keys)) { $settings[$key] = $base[$key] }
    $settings['Publishing__Verifier__MaxAssets'] = '1024'
    $settings['Publishing__Verifier__MaxDocumentBytes'] = '4194304'
    $settings['Publishing__Verifier__TimeoutSeconds'] = '45'
    $settings['Publishing__Intake__AssemblyMinutes'] = '10'
}
$allowed = '^Publishing__(Verifier__(Provider|CapacityProfile|SessionPoolResourceId|Image|ReaderCommit|MemoryMiB|ScratchMiB|MaxAssets|MaxAssetBytes|MaxDocumentBytes|TimeoutSeconds)|Channels__arke-worlds-dev__MaxPackageBytes|Intake__(MaxCreatorRetainedBytes|MaxTenantRetainedBytes|MaxCreatorPackages|AssemblyMinutes|PartSizeBytes))$'
if (@($settings.Keys | Where-Object { $_ -notmatch $allowed }).Count) { throw 'Only bounded dev capacity settings may change.' }
$token = az account get-access-token --resource https://management.azure.com --query accessToken -o tsv
if ($LASTEXITCODE -ne 0 -or !$token) { throw 'Azure authentication failed.' }
try {
    $headers = @{ Authorization = "Bearer $token" }
    if ($Profile -eq 'large-v1') {
        $shareUri = "https://management.azure.com/subscriptions/$subscription/resourceGroups/rg-aonik-dev-uksouth/providers/Microsoft.Storage/storageAccounts/ankpubxkhim4xqiopna/fileServices/default/shares/intake?api-version=2023-05-01"
        $share = Invoke-RestMethod -Uri $shareUri -Headers $headers
        if ($share.properties.shareQuota -lt 32) { throw 'Deploy the dev intake share quota of 32 GiB before enabling large capacity.' }
    }
    $plans = foreach ($service in @('api','worker')) {
        $name = "aonik-dev-$service"
        $uri = "https://management.azure.com/subscriptions/$subscription/resourceGroups/rg-aonik-dev-uksouth/providers/Microsoft.App/containerApps/${name}?api-version=2024-03-01"
        $app = Invoke-RestMethod -Uri $uri -Headers $headers
        if ($app.properties.provisioningState -ne 'Succeeded' -or @($app.properties.template.containers).Count -ne 1) { throw "$name is not ready." }
        $template = $app.properties.template | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
        $container = $template.containers[0]
        $envByName = [ordered]@{}
        foreach ($entry in $container.env) { $envByName[$entry.name] = $entry }
        if ($envByName['Publishing__Channels__arke-worlds-dev__TenantId'].value -ne '1087cc03-46cc-45fa-9023-f0ce4f7bbdb8') { throw 'Wrong dev channel tenant.' }
        foreach ($key in $settings.Keys) {
            if ($null -eq $settings[$key]) { $envByName.Remove($key) }
            else { $envByName[$key] = @{ name=$key; value=[string]$settings[$key] } }
        }
        $container.env = @($envByName.Values)
        @{ name=$name; uri=$uri; location=$app.location; template=$template; previousRevision=$app.properties.latestReadyRevisionName }
    }
    foreach ($plan in $plans) {
        if ($Apply) {
            # Preserve unrelated settings and secret references entirely in memory.
            $body = @{ location=$plan.location; properties=@{template=$plan.template} } | ConvertTo-Json -Depth 100 -Compress
            try { $null = Invoke-RestMethod -Uri $plan.uri -Method Patch -ContentType 'application/json' -Body $body -Headers $headers }
            catch { throw "Dev capacity update failed for $($plan.name); inspect Azure state before retrying." }
        }
        [pscustomobject]@{ app=$plan.name; capacityProfile=$Profile; previousRevision=$plan.previousRevision; applied=[bool]$Apply }
    }
} finally { $token=$null; $headers=$null; $plans=$null; $app=$null; $template=$null; $container=$null; $envByName=$null; $body=$null }
