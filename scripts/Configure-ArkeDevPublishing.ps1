param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$subscription = '336d38e2-bb96-4510-a4c5-5c313139eef9'
$resourceGroup = 'rg-aonik-dev-uksouth'
$root = Split-Path -Parent $PSScriptRoot
$profile = Get-Content -LiteralPath (Join-Path $root 'iac/azure/environments/dev/aca.parameters.json') -Raw | ConvertFrom-Json -AsHashtable
$settings = $profile.parameters.publishingAppSettings.value

if (!$profile.parameters.publishingStorageEnabled.value -or !$settings.Count) {
    throw 'The dev publishing storage/profile is not configured.'
}
foreach ($name in $settings.Keys) {
    if (!$name.StartsWith('Publishing__', [StringComparison]::Ordinal)) {
        throw 'Only Publishing__ settings are accepted by this dev setup script.'
    }
}
$channel = 'Publishing__Channels__arke-worlds-dev__'
foreach ($flag in @('Enabled', 'NamespaceAdmissionEnabled', 'ReviewEnabled', 'DeliveryEnabled', 'PublicCatalogueEnabled', 'LegacyMirrorEnabled')) {
    if ($settings[$channel + $flag] -cne 'false') {
        throw 'This script only stages a closed dev channel. Enablement requires a separately qualified change.'
    }
}
if ($settings[$channel + 'TenantId'] -ne '1087cc03-46cc-45fa-9023-f0ce4f7bbdb8') {
    throw 'The dev tenant differs from the inspected Arke Kidz tenant.'
}
$baselineHash = [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($settings[$channel + 'BaselineCatalogueJson'])))
if ($baselineHash -cne $settings[$channel + 'BaselineSha256']) { throw 'Baseline hash mismatch.' }
$mounts = @(
    @{volumeName='publishing-intake'; mountPath='/mnt/aonik-publishing-private'},
    @{volumeName='publishing-delivery'; mountPath='/mnt/aonik-publishing-public'}
)
if ($settings['Publishing__Intake__PrivateRoot'] -cne $mounts[0].mountPath -or
    $settings['Publishing__Delivery__PublicRoot'] -cne $mounts[1].mountPath) {
    throw 'Publishing roots must match the qualified absolute mount paths.'
}

$activeSubscription = az account show --query id --output tsv
if ($LASTEXITCODE -ne 0 -or $activeSubscription -ne $subscription) { throw 'Select the expected dev subscription first.' }
$token = az account get-access-token --resource https://management.azure.com --query accessToken --output tsv
if ($LASTEXITCODE -ne 0 -or !$token) { throw 'Azure authentication failed.' }

try {
    $headers = @{Authorization="Bearer $token"}
    $base = "https://management.azure.com/subscriptions/$subscription/resourceGroups/$resourceGroup/providers/Microsoft.App"
    # Read and validate both apps and both storage bindings before any write.
    foreach ($mount in $mounts) {
        $url = "$base/managedEnvironments/aonik-dev-acae/storages/$($mount.volumeName)?api-version=2024-03-01"
        $storage = Invoke-RestMethod -Uri $url -Headers $headers
        if ($storage.properties.azureFile.accessMode -ne 'ReadWrite' -or
            $storage.properties.azureFile.accountName -ne 'ankpubxkhim4xqiopna') {
            throw 'Deploy and inspect the dedicated publishing-storage module first.'
        }
    }
    $plans = foreach ($service in @('api', 'worker')) {
        $name = "aonik-dev-$service"
        $url = "$base/containerApps/${name}?api-version=2024-03-01"
        $app = Invoke-RestMethod -Uri $url -Headers $headers
        if ($app.properties.provisioningState -ne 'Succeeded') { throw "$name is not ready for a configuration update." }
        $template = $app.properties.template | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
        $container = @($template.containers | Where-Object name -eq $service)
        if ($container.Count -ne 1) { throw "Unexpected container layout in $name." }
        $container = $container[0]
        $envByName = [ordered]@{}
        foreach ($entry in $container.env) {
            if ($entry.name.StartsWith('Publishing__', [StringComparison]::OrdinalIgnoreCase) -and !$settings.Contains($entry.name)) {
                throw "$name has unrecognised publishing settings; reconcile them explicitly first."
            }
            $envByName[$entry.name] = $entry
        }
        foreach ($key in $settings.Keys) { $envByName[$key] = @{name=$key; value=[string]$settings[$key]} }
        $container.env = @($envByName.Values)
        $existingMounts = @($container.volumeMounts | Where-Object { $_ -and $_.volumeName -notin $mounts.volumeName })
        if (@($existingMounts | Where-Object mountPath -in $mounts.mountPath).Count) { throw 'Another volume already owns a publishing path.' }
        $container.volumeMounts = @($existingMounts) + $mounts
        $volumes = @($template.volumes | Where-Object { $_ -and $_.name -notin $mounts.volumeName })
        foreach ($mount in $mounts) {
            # This existing ACA environment rejects custom SMB mount options.
            # Access to the share is controlled by its private storage binding.
            $volumes += @{name=$mount.volumeName; storageType='AzureFile'; storageName=$mount.volumeName}
        }
        $template.volumes = $volumes
        $template.scale.minReplicas = [Math]::Max(1, [int]$template.scale.minReplicas)
        @{name=$name; url=$url; location=$app.location; template=$template; revision=$app.properties.latestReadyRevisionName}
    }
    foreach ($plan in $plans) {
        if ($Apply) {
            # Keep the existing template and credentials in memory. Never serialize
            # them to a temp file, command argument, deployment log or stdout.
            $body = @{location=$plan.location; properties=@{template=$plan.template}} | ConvertTo-Json -Depth 100 -Compress
            try {
                $result = Invoke-RestMethod -Uri $plan.url -Method Patch -ContentType 'application/json' -Body $body -Headers $headers
            } catch {
                $failureCode = 'unknown'
                try { $failureCode = ($_.ErrorDetails.Message | ConvertFrom-Json).error.code } catch { }
                throw "Configuration update failed for $($plan.name), HTTP $([int]$_.Exception.Response.StatusCode), code $failureCode. Inspect Azure deployment state before retrying."
            }
            [pscustomobject]@{app=$plan.name; previousRevision=$plan.revision;
                state=$(if ($result.properties.provisioningState) {$result.properties.provisioningState} else {'Accepted'}); channel='closed'}
        } else {
            [pscustomobject]@{app=$plan.name; revision=$plan.revision; settingCount=$settings.Count; mountPaths=$mounts.mountPath;
                minReplicas=$plan.template.scale.minReplicas; channel='closed'; mode='preview'}
        }
    }
} finally {
    $token=$null; $headers=$null; $body=$null; $plans=$null; $app=$null; $container=$null; $envByName=$null
}
