param(
    [Parameter(Mandatory)][string]$SdkRoot,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$CatalogSha256,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$Development,
    [switch]$Locked,
    [string]$InspectorPackageVersion,
    [string]$NuGetConfig,
    [string]$PublishDirectory
)
$ErrorActionPreference = 'Stop'
if ($Development -and $PublishDirectory) { throw 'Publish requires the public Editor build.' }
if (-not [IO.Path]::IsPathFullyQualified($SdkRoot)) { throw 'Provide an absolute installed SDK path.' }
$manifest = Join-Path $SdkRoot 'sdk.json'
if ((Get-FileHash -LiteralPath $manifest).Hash -ine $CatalogSha256) { throw 'SDK catalog hash mismatch.' }
$catalog = Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json
$distribution = if ($Development) { 'local-private' } else { 'public' }
if ($catalog.distributionClass -ne $distribution -or -not $catalog.features.managedBindingInputs) {
    throw "The selected SDK must provide $distribution managed binding inputs."
}
$pins = @{
    Binding = 'bindings/dotnet/PixieCapi.cs'
    Props = 'bindings/dotnet/NativeApiVersion.props'
    Metadata = 'bindings/dotnet/NativeApiVersion.cs'
    Header = 'include/pixie_capi.h'
}
$dev = $Development.IsPresent.ToString().ToLowerInvariant()
$flags = @("-p:PixieBuildSdkRoot=$SdkRoot","-p:PixieBuildSdkCatalogSha256=$CatalogSha256",
    "-p:PixieDevelopmentTools=$dev","-p:Configuration=$Configuration",'-p:RestorePackagesWithLockFile=true')
foreach ($name in $pins.Keys) {
    $filePins = @($catalog.files | Where-Object { $_.path -ceq $pins[$name] })
    if ($filePins.Count -ne 1 -or $filePins[0].sha256 -notmatch '^[A-Fa-f0-9]{64}$') {
        throw "The SDK catalog must pin $($pins[$name])."
    }
    $flags += "-p:PixieBuildSdk$($name)Sha256=$($filePins[0].sha256)"
}
$repository = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repository 'src/PixieEditor.csproj'
if ($InspectorPackageVersion) {
    $flags += "-p:PixieInspectorPackageVersion=$InspectorPackageVersion"
    $flags += "-p:NuGetLockFilePath=$(Join-Path $repository 'src/packages.inspector-selected.lock.json')"
}
$restoreFlags = @()
if ($NuGetConfig) { $restoreFlags += @('--configfile',(Resolve-Path -LiteralPath $NuGetConfig).Path) }
if ($Locked) { $restoreFlags += '--locked-mode' }
if ($PublishDirectory) { $PublishDirectory = [IO.Path]::GetFullPath($PublishDirectory, (Get-Location).Path) }
Push-Location -LiteralPath $repository
try {
    & dotnet restore $project @flags @restoreFlags
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($PublishDirectory) {
        & dotnet publish $project --no-restore -o $PublishDirectory @flags
    } else {
        & dotnet build $project --no-restore @flags
    }
    exit $LASTEXITCODE
} finally {
    Pop-Location
}

