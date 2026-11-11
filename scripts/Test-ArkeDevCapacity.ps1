param(
    [Parameter(Mandatory)][string]$ArchivePath,
    [Parameter(Mandatory)][string]$ExpectedImage,
    [Parameter(Mandatory)][string]$EvidencePath,
    [ValidateRange(1,2048)][int]$MaxAssets = 2048,
    [ValidateRange(1,180)][int]$TimeoutSeconds = 180,
    [ValidateRange(0,384)][int]$StopAfterChunks = 0,
    [string]$ExpectedRefusal
)

$ErrorActionPreference = 'Stop'
$subscription = '336d38e2-bb96-4510-a4c5-5c313139eef9'
$poolId = "/subscriptions/$subscription/resourceGroups/rg-aonik-dev-uksouth/providers/Microsoft.App/sessionPools/aonikdevpublishinglarge"
$apiVersion = '2025-02-02-preview'
if ((az account show --query id -o tsv) -ne $subscription) { throw 'Select the dev subscription.' }
if ($ExpectedImage -notmatch '^aonikdevacr[.]azurecr[.]io/aonik-arke-verifier-sessions@sha256:[a-f0-9]{64}$') { throw 'Use an immutable dev verifier image.' }
$pool = az rest --method get --url "https://management.azure.com${poolId}?api-version=$apiVersion" -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $pool.properties.customContainerTemplate.containers[0].image -cne $ExpectedImage -or
    $pool.properties.templateUpdateStatus.activeTemplate.containers[0].image -cne $ExpectedImage -or
    $pool.properties.sessionNetworkConfiguration.status -ne 'EgressDisabled' -or
    $pool.properties.managedIdentitySettings[0].lifecycle -ne 'None') { throw 'The dev verifier differs from its qualified image/policy.' }
$endpoint = $pool.properties.poolManagementEndpoint.TrimEnd('/')
if ($endpoint -notmatch '^https://aonikdevpublishinglarge[.][a-z0-9.-]+[.]azurecontainerapps[.]io$') { throw 'Unexpected dev session endpoint.' }
$file = Get-Item -LiteralPath $ArchivePath
$hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$token = az account get-access-token --resource https://dynamicsessions.io --query accessToken -o tsv
if ($LASTEXITCODE -ne 0 -or !$token) { throw 'Cannot authenticate to the dev session pool.' }
$nonce = [Convert]::ToHexStringLower([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$session = 'capacity-428-' + [Guid]::NewGuid().ToString('N')
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [Threading.Timeout]::InfiniteTimeSpan
$deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(900))
$clock = [Diagnostics.Stopwatch]::StartNew()
$evidence = [ordered]@{utc=[DateTime]::UtcNow.ToString('o');image=$ExpectedImage;archiveSha256=$hash;archiveBytes=$file.Length;profile='large-v1'}

function Send-Session([string]$Operation, [Net.Http.HttpContent]$Content, [long]$Offset = -1) {
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, "$endpoint/$Operation`?identifier=$session&api-version=$apiVersion")
    $request.Headers.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)
    $request.Headers.Add('X-Verification-Nonce', $nonce)
    if ($Offset -ge 0) { $request.Headers.Add('X-Upload-Offset', $Offset.ToString([Globalization.CultureInfo]::InvariantCulture)) }
    $request.Content = $Content
    try {
        $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $deadline.Token).GetAwaiter().GetResult()
        try {
            $stream = $response.Content.ReadAsStreamAsync($deadline.Token).GetAwaiter().GetResult()
            $buffer = [byte[]]::new(8192)
            $body = [IO.MemoryStream]::new()
            try {
                while (($count = $stream.ReadAsync($buffer, 0, $buffer.Length, $deadline.Token).GetAwaiter().GetResult()) -gt 0) {
                    if ($body.Length + $count -gt 1056768) { throw 'The response exceeded its bound.' }
                    $body.Write($buffer, 0, $count)
                }
                $text = [Text.Encoding]::UTF8.GetString($body.ToArray())
                $value = if ($text) { $text | ConvertFrom-Json -Depth 50 } else { $null }
                if (!$response.IsSuccessStatusCode) {
                    $code = if ($value.code -match '^[a-z0-9-]{1,100}$') { $value.code } else { 'http-' + [int]$response.StatusCode }
                    throw "sandbox-refused:$code"
                }
                return $value
            } finally { $body.Dispose(); $stream.Dispose() }
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }
}

try {
    $declaration = @{nonce=$nonce;sha256=$hash;bytes=$file.Length;maxAssets=$MaxAssets;maxAssetBytes=67108864;maxDocumentBytes=4194304;timeoutSeconds=$TimeoutSeconds} | ConvertTo-Json -Compress
    $ack = Send-Session 'begin' ([Net.Http.StringContent]::new($declaration, [Text.Encoding]::UTF8, 'application/json'))
    if ($ack.nonce -cne $nonce -or $ack.offset -ne 0 -or $ack.capacityProfile -cne 'large-v1') { throw 'Wrong sandbox declaration acknowledgement.' }
    $archiveStream = [IO.File]::OpenRead($file.FullName)
    try {
        $buffer = [byte[]]::new(4194304)
        [long]$offset = 0
        $chunks = 0
        while (($count = $archiveStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $content = [Net.Http.ByteArrayContent]::new($buffer, 0, $count)
            $content.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::new('application/octet-stream')
            $ack = Send-Session 'chunk' $content $offset
            $offset += $count
            if ($ack.nonce -cne $nonce -or $ack.offset -ne $offset) { throw 'Wrong sandbox chunk acknowledgement.' }
            $chunks++
            if ($StopAfterChunks -gt 0 -and $chunks -eq $StopAfterChunks) { break }
        }
    } finally { $archiveStream.Dispose() }
    $evidence.uploadSeconds = [Math]::Round($clock.Elapsed.TotalSeconds, 3)
    $evidence.chunks = $chunks
    if ($StopAfterChunks -gt 0) { $evidence.cancelledAfterBytes = $offset }
    else {
        $started = Send-Session 'verify' ([Net.Http.ByteArrayContent]::new([byte[]]::new(0)))
        if ($started.nonce -cne $nonce -or $started.status -cne 'running') { throw 'Wrong verification acknowledgement.' }
        do {
            $report = Send-Session 'result' ([Net.Http.ByteArrayContent]::new([byte[]]::new(0)))
            if ($report.nonce -cne $nonce) { throw 'Wrong verification provenance.' }
            if ($report.result) { break }
            if ($report.status -cne 'running') { throw 'Wrong verification status.' }
            Start-Sleep -Milliseconds 500
        } while (!$deadline.IsCancellationRequested)
        if (!$report.result) { throw 'Verification exceeded its overall deadline.' }
        $evidence.resultCode = $report.result.code
        $evidence.metrics = $report.metrics
        $evidence.assetCount = $report.result.assets.Count
        $evidence.reportBytes = [Text.Encoding]::UTF8.GetByteCount(($report.result | ConvertTo-Json -Compress -Depth 50))
        if ($ExpectedRefusal) {
            if ($report.result.passed -or $report.result.code -cne $ExpectedRefusal) { throw 'The expected refusal was not returned.' }
        } elseif (!$report.result.passed -or $report.result.archiveSha256 -cne $hash -or $report.result.archiveBytes -ne $file.Length) {
            throw 'The sandbox did not verify the exact input.'
        }
    }
} catch {
    if ($ExpectedRefusal -and $_.Exception.Message -eq "sandbox-refused:$ExpectedRefusal") { $evidence.resultCode = $ExpectedRefusal }
    else { $evidence.failure = $_.Exception.Message; throw }
} finally {
    # Reclamation has its own deadline even when the verification deadline expired.
    $deadline.Dispose()
    $deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(15))
    try {
        $null = Send-Session '.management/stopSession' ([Net.Http.ByteArrayContent]::new([byte[]]::new(0)))
        $evidence.cleanupAcknowledged = $true
    } catch { $evidence.cleanupAcknowledged = $false; throw }
    finally {
        $evidence.totalSeconds = [Math]::Round($clock.Elapsed.TotalSeconds, 3)
        $evidence | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $EvidencePath
        $client.Dispose(); $deadline.Dispose(); $token = $null
    }
}
$evidence | ConvertTo-Json -Depth 15
