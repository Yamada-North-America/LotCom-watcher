param(
    [Parameter(Mandatory)]
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')]
    [string]$Version
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '../..')
$root = (Get-Location).Path
$publish = Join-Path $root 'artifacts/publish'
$packages = Join-Path $root 'artifacts/packages'
$msiOutput = Join-Path $root 'artifacts/msi'
$project = 'LotComWatcher/LotComWatcher.csproj'
if (!(Test-Path '../LotCom-libraries/LotCom/LotCom.csproj')) {
    throw 'Check out LotCom-libraries alongside LotCom-watcher before building.'
}
if (Test-Path 'artifacts') { Remove-Item 'artifacts' -Recurse -Force }
New-Item $publish, $packages, $msiOutput -ItemType Directory -Force | Out-Null

# The Worker SDK project needs the Windows targeting packs, not MAUI/WinUI workloads.
dotnet restore $project --runtime win-x64 -p:SelfContained=true -p:Configuration=Release
if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
dotnet publish $project --configuration Release --framework net9.0-windows10.0.19041.0 `
    --runtime win-x64 --self-contained true --no-restore "-p:Version=$Version" `
    "-p:ApplicationDisplayVersion=$Version" -p:ContinuousIntegrationBuild=true --output $publish
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
foreach ($file in @('LotComWatcher.exe', 'appsettings.json')) {
    if (!(Test-Path (Join-Path $publish $file))) { throw "Published payload is missing $file." }
}

# Build the adapted original WixSharp setup; do not include setup output in the payload.
dotnet build 'installer/LotComWatcher.Setup.csproj' --configuration Release -p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) { throw 'Setup project build failed.' }
& './installer/bin/Release/net472/LotComWatcher.Setup.exe' $Version $publish $msiOutput
if ($LASTEXITCODE -ne 0) { throw 'MSI build failed.' }
$msi = Join-Path $msiOutput "LotComWatcher_$Version.msi"
if (!(Test-Path $msi)) { throw "Expected MSI was not generated: $msi" }
Copy-Item $msi $packages
[IO.Compression.ZipFile]::CreateFromDirectory($publish, (Join-Path $packages "LotComWatcher_$Version`_win-x64.zip"))
$checksums = Get-ChildItem $packages -File | Sort-Object Name | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
$checksums | Set-Content (Join-Path $packages 'SHA256SUMS.txt') -Encoding utf8
