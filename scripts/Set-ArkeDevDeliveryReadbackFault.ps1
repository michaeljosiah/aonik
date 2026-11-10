#Requires -Version 7.0

param(
    [Parameter(Mandatory)][ValidateSet('Preview', 'Block', 'Restore')][string]$Action,
    [string]$WorkerSourceIp,
    [string]$StatePath = (Join-Path $PSScriptRoot '../output/research/403-delivery-fault-state.json'),
    [ValidateRange(60, 3600)][int]$AutoRestoreSeconds = 600,
    [ValidateRange(0, 3600)][int]$RestoreAfterSeconds = 0,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$subscription = '336d38e2-bb96-4510-a4c5-5c313139eef9'
$resourceGroup = 'rg-aonik-dev-uksouth'
$appName = 'aonik-dev-api'
$ruleName = 'arke-dev-403-readback-rehearsal'
$description = 'Temporary owner-authorised dev delivery readback rehearsal; restore recorded baseline.'
$stateFile = [IO.Path]::GetFullPath($StatePath)

function Save-State($State) {
    $State.updatedAt = [DateTime]::UtcNow.ToString('o')
    $temporaryState = $stateFile + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $State | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $temporaryState -Encoding utf8
    [IO.File]::Move($temporaryState, $stateFile, $true)
}
function Read-Rules {
    $json = az containerapp ingress access-restriction list --subscription $subscription --name $appName --resource-group $resourceGroup --output json --only-show-errors
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the dev API ingress restrictions.' }
    @($json | ConvertFrom-Json -AsHashtable)
}

if ($Action -eq 'Restore' -and $RestoreAfterSeconds -gt 0) {
    # Detached watchdog: short sleeps let an explicit restore stop it promptly.
    $deadline = [DateTime]::UtcNow.AddSeconds($RestoreAfterSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $stateFile) {
            $checkpoint = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json -AsHashtable
            if ($checkpoint.restored -eq $true) { exit 0 }
        }
        Start-Sleep -Seconds 5
    }
}

if ($Action -eq 'Restore') {
    $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json -AsHashtable
    if ($state.subscription -ne $subscription -or $state.resourceGroup -ne $resourceGroup -or
        $state.appName -ne $appName -or $state.ruleName -ne $ruleName -or @($state.originalRules).Count -ne 0) {
        throw 'The restore record does not describe this isolated dev rehearsal.'
    }
    $rules = @(Read-Rules)
    $owned = @($rules | Where-Object name -eq $ruleName)
    if ($owned.Count -gt 1 -or ($owned.Count -eq 1 -and
        ($owned[0].action -ne 'Deny' -or $owned[0].ipAddressRange -ne $state.ipAddressRange -or $owned[0].description -ne $description))) {
        throw 'The temporary rule changed; inspect it instead of overwriting another operator.'
    }
    if (!$Apply) { [pscustomobject]@{action='restore-preview'; app=$appName; ownedRules=$owned.Count}; exit 0 }
    if ($owned.Count -eq 1) {
        az containerapp ingress access-restriction remove --subscription $subscription --name $appName --resource-group $resourceGroup --rule-name $ruleName --output none --only-show-errors
        if ($LASTEXITCODE -ne 0) { throw 'Temporary rule removal failed; restore it with the documented CLI command.' }
    }
    $remaining = @(Read-Rules)
    if (@($remaining | Where-Object name -eq $ruleName).Count) { throw 'The rehearsal rule still exists.' }
    $state.restored = $true
    $state.restoredAt = [DateTime]::UtcNow.ToString('o')
    $state.originalBaselineRestored = $remaining.Count -eq 0
    Save-State $state
    [pscustomobject]@{action='restored'; app=$appName; originalBaselineRestored=$state.originalBaselineRestored}
    exit 0
}

$address = $null
if (![Net.IPAddress]::TryParse($WorkerSourceIp, [ref]$address) -or
    $address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or
    !$WorkerSourceIp.StartsWith('100.100.', [StringComparison]::Ordinal)) {
    throw 'Supply the single Worker source IPv4 observed in the current dev debug-console probe (100.100.x.x). Never supply a network range.'
}
$rules = @(Read-Rules)
if ($rules.Count -ne 0) { throw 'Dev ingress already has restrictions; do not replace or combine them for this rehearsal.' }
$apiJson = az containerapp show --subscription $subscription --name $appName --resource-group $resourceGroup --query '{revision:properties.latestReadyRevisionName,state:properties.provisioningState,fqdn:properties.configuration.ingress.fqdn}' --output json --only-show-errors
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the dev API.' }
$api = $apiJson | ConvertFrom-Json -AsHashtable
if ($api.state -ne 'Succeeded' -or $api.fqdn -ne 'aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io') { throw 'Unexpected dev API state or host.' }
$workerJson = az containerapp replica list --subscription $subscription --name aonik-dev-worker --resource-group $resourceGroup --query '[].name' --output json --only-show-errors
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Worker replicas.' }
$replicas = @($workerJson | ConvertFrom-Json)
if ($replicas.Count -ne 1) { throw 'Qualify every Worker source address before testing a multi-replica deployment.' }
$plan = [ordered]@{subscription=$subscription; resourceGroup=$resourceGroup; appName=$appName; ruleName=$ruleName;
    ipAddressRange=$address.ToString()+'/32'; originalRules=@(); apiRevision=$api.revision; workerReplica=$replicas[0];
    createdAt=[DateTime]::UtcNow.ToString('o'); autoRestoreSeconds=$AutoRestoreSeconds; restored=$false}
if ($Action -eq 'Preview' -or !$Apply) {
    [pscustomobject]@{action='preview'; app=$appName; workerReplica=$replicas[0]; deniedSource=$plan.ipAddressRange; originalRuleCount=0; autoRestoreSeconds=$AutoRestoreSeconds}
    exit 0
}
if (Test-Path -LiteralPath $stateFile) {
    $previous = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json -AsHashtable
    if (!$previous.restored) { throw 'An earlier rehearsal is unresolved; restore or inspect its state first.' }
    if ($previous.watchdogPid -and (Get-Process -Id $previous.watchdogPid -ErrorAction SilentlyContinue)) {
        throw 'The earlier watchdog has not exited. Wait for it to stop or use a fresh state path before starting another rehearsal.'
    }
}
if (!(Test-Path -LiteralPath (Split-Path -Parent $stateFile))) { throw 'Create the private evidence directory before applying the rehearsal.' }
Save-State $plan
$watchdog = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList @('-NoProfile', '-File', ('"'+$PSCommandPath+'"'),
    '-Action', 'Restore', '-StatePath', ('"'+$stateFile+'"'), '-RestoreAfterSeconds', $AutoRestoreSeconds, '-Apply') -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput ($stateFile+'.watchdog.out.log') -RedirectStandardError ($stateFile+'.watchdog.err.log')
$plan.watchdogPid = $watchdog.Id
Save-State $plan
az containerapp ingress access-restriction set --subscription $subscription --name $appName --resource-group $resourceGroup --rule-name $ruleName --ip-address $plan.ipAddressRange --action Deny --description $description --output none --only-show-errors
if ($LASTEXITCODE -ne 0) { throw 'Rule creation did not complete; inspect state and run Restore. The detached watchdog is also armed.' }
$applied = @(Read-Rules)
if ($applied.Count -ne 1 -or $applied[0].name -ne $ruleName -or $applied[0].ipAddressRange -ne $plan.ipAddressRange -or $applied[0].action -ne 'Deny') { throw 'Applied ingress rule differs from the intended single-source denial.' }
$plan.appliedAt = [DateTime]::UtcNow.ToString('o')
Save-State $plan
[pscustomobject]@{action='blocked'; app=$appName; deniedSource=$plan.ipAddressRange; watchdogPid=$watchdog.Id; autoRestoreSeconds=$AutoRestoreSeconds}
