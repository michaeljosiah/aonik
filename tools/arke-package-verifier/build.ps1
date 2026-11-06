param(
  [Parameter(Mandatory=$true)][string]$PlayerRepository,
  [Parameter(Mandatory=$true)][string]$OutputDirectory,
  [string]$Dart = 'dart',
  [switch]$AllowNetwork
)
$ErrorActionPreference = 'Stop'
$pin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'reader-pin.json') -Raw | ConvertFrom-Json
$readerSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'lib/verify.dart') -Raw
if (-not $readerSource.Contains("const readerCommit = '$($pin.commit)';")) { throw 'Worker provenance differs from the pinned reader source' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$stage = Join-Path $output ('build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$archive = Join-Path $stage 'reader.tar'
& git -C $PlayerRepository archive --format=tar --output=$archive $pin.commit packages/arke_world packages/arke_library packages/arke_media packages/arke_publication pubspec.lock
if ($LASTEXITCODE -ne 0) { throw 'The pinned Player reader could not be archived' }
& tar -xf $archive -C $stage
if ($LASTEXITCODE -ne 0) { throw 'The pinned archive could not be extracted' }
$worker = Join-Path $stage 'verifier'
New-Item -ItemType Directory -Path $worker | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'reader-pin.json') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Dockerfile'),(Join-Path $PSScriptRoot '.dockerignore') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Dockerfile.sessions'),(Join-Path $PSScriptRoot 'session') -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin'),(Join-Path $PSScriptRoot 'lib'),(Join-Path $PSScriptRoot 'pubspec.yaml') -Destination $worker -Recurse
if (Test-Path (Join-Path $PSScriptRoot 'test')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'test') -Destination $worker -Recurse }
@'
name: aonik_verifier_workspace
publish_to: none
environment:
  sdk: ^3.11.1
workspace:
  - packages/arke_world
  - packages/arke_library
  - packages/arke_media
  - packages/arke_publication
  - verifier
'@ | Set-Content -LiteralPath (Join-Path $stage 'pubspec.yaml')
Push-Location $stage
try {
  if ($AllowNetwork) { & $Dart pub get } else { & $Dart pub get --offline }
  if ($LASTEXITCODE -ne 0) { throw 'Dependency resolution failed; no unpinned checkout is used as a fallback' }
  & $Dart analyze verifier
  if ($LASTEXITCODE -ne 0) { throw 'Verifier analysis failed' }
  if (Test-Path (Join-Path $worker 'test')) {
    Push-Location $worker
    try { & $Dart test; if ($LASTEXITCODE -ne 0) { throw 'Verifier fixture tests failed' } }
    finally { Pop-Location }
  }
  $executable = Join-Path $output ('arke-package-verifier' + $(if ($IsWindows) { '.exe' } else { '' }))
  & $Dart compile exe verifier/bin/verify.dart -o $executable
  if ($LASTEXITCODE -ne 0) { throw 'Verifier compilation failed' }
  @{readerCommit=$pin.commit;protocolVersion=$pin.protocolVersion;executableSha256=(Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant();buildDirectory=$stage} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-info.json')
} finally { Pop-Location }
