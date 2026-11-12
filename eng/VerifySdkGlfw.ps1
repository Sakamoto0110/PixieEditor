param(
    [Parameter(Mandatory)][string]$SdkRoot,
    [Parameter(Mandatory)][string]$CatalogSha256,
    [Parameter(Mandatory)][ValidateSet('authoring','local-development')][string]$Profile,
    [Parameter(Mandatory)][ValidateSet('Debug','Release')][string]$Configuration
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1')
$catalogPath = Join-Path $SdkRoot 'sdk.json'
if ((Get-FileHash -LiteralPath $catalogPath).Hash -ine $CatalogSha256) {
    throw 'SDK catalog hash mismatch.'
}
$catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
$tuple = "profiles/$Profile/$Configuration"
$profilePath = "$tuple/profile.json"
$entries = @($catalog.profiles | Where-Object { $_.profile -ceq $Profile -and $_.configuration -ceq $Configuration })
if ($entries.Count -ne 1 -or $entries[0].manifest -cne $profilePath -or
    (Get-FileHash -LiteralPath (Join-Path $SdkRoot $profilePath)).Hash -ine $entries[0].manifestSha256) {
    throw "Cannot verify the selected SDK profile $Profile/$Configuration."
}
$manifest = Get-Content -Raw -LiteralPath (Join-Path $SdkRoot $profilePath) | ConvertFrom-Json
$dll = "$tuple/bin/glfw3.dll"
$provider = @($manifest.nativeDependencies | Where-Object path -CEQ $dll)
$inventory = @($catalog.files | Where-Object path -CEQ $dll)
if ($provider.Count -ne 1 -or $inventory.Count -ne 1 -or $provider[0].sha256 -cne $inventory[0].sha256 -or
    (Get-FileHash -LiteralPath (Join-Path $SdkRoot $dll)).Hash -ine $provider[0].sha256) {
    throw "GLFW does not match the selected SDK profile $Profile/$Configuration."
}
$notice = 'notices/glfw/LICENSE.md'
$notices = @($catalog.files | Where-Object path -CEQ $notice)
if ($notices.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $SdkRoot $notice)).Hash -ine $notices[0].sha256) {
    throw 'GLFW notice does not match the selected SDK catalog.'
}
$provider[0].sha256
