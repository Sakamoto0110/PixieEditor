param(
    [Parameter(Mandatory)][string]$SdkRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$EngineSource,
    [Parameter(Mandatory)][string]$BaselineNative,
    [Parameter(Mandatory)][string]$Out
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$env:VSLANG = '1033'
$pins = Get-Content -Raw "$PSScriptRoot/pins.json" | ConvertFrom-Json -AsHashtable
$outDir = (New-Item -ItemType Directory -Path $Out).FullName
$inputs = (New-Item -ItemType Directory -Path "$outDir/inputs").FullName
$binaries = (New-Item -ItemType Directory -Path "$outDir/bin").FullName

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-PinnedSource($Pin, [string]$Path) {
    git init -q $Path
    git -C $Path -c advice.detachedHead=false fetch -q --depth 1 $Pin.repository $Pin.commit
    git -C $Path -c core.autocrlf=false -c advice.detachedHead=false checkout -q --detach FETCH_HEAD
    if ((git -C $Path rev-parse HEAD) -cne $Pin.commit) { throw "Source pin mismatch: $($Pin.repository)" }
}

function Get-Exports([string]$Path) {
    @(& dumpbin.exe /nologo /exports $Path | ForEach-Object {
        if ($_ -match '^\s+\d+\s+[0-9A-F]+\s+[0-9A-F]+\s+(\S+)\s*$') { $Matches[1] }
    } | Sort-Object -CaseSensitive)
}

function Get-Imports([string]$Path) {
    @(& dumpbin.exe /nologo /dependents $Path | ForEach-Object {
        if ($_ -match '^\s+(\S+\.dll)\s*$') { $Matches[1].ToLowerInvariant() }
    } | Sort-Object -Unique)
}

$vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'MSVC x64 tools are required.' }
& "$vs/Common7/Tools/Launch-VsDevShell.ps1" -Arch amd64 -HostArch amd64 -SkipAutomaticLocation
if ($env:VCToolsVersion.TrimEnd('\') -cne $pins.msvcTools) {
    throw "Expected MSVC tools $($pins.msvcTools), found $env:VCToolsVersion."
}

$kit = "${env:ProgramFiles(x86)}/Windows Kits/10"
$winSdk = $pins.windowsSdk
if (-not (Test-Path -LiteralPath "$kit/Include/$winSdk/ucrt/stdio.h")) { throw "Windows SDK $winSdk is required." }
$env:INCLUDE = "$kit/Include/$winSdk/ucrt;$kit/Include/$winSdk/um;$kit/Include/$winSdk/shared;$env:INCLUDE"
$env:LIB = "$kit/Lib/$winSdk/ucrt/x64;$kit/Lib/$winSdk/um/x64;$env:LIB"
$env:Path = "$kit/bin/$winSdk/x64;$env:Path"
$cmake = "$vs/Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe"
$ninja = "$vs/Common7/IDE/CommonExtensions/Microsoft/CMake/Ninja/ninja.exe"
if (-not (Test-Path -LiteralPath $cmake)) { $cmake = (Get-Command cmake.exe).Source }
if (-not (Test-Path -LiteralPath $ninja)) { $ninja = (Get-Command ninja.exe).Source }
$vcpkg = "$vs/VC/vcpkg/vcpkg.exe"

$cimgui = "$inputs/cimgui"
$backend = "$inputs/backend"
$lua = "$inputs/luajit"
Get-PinnedSource $pins.sources.cimgui $cimgui
Get-PinnedSource $pins.sources.cimgui_impl $backend
Get-PinnedSource $pins.sources.imgui "$cimgui/imgui"
Get-PinnedSource $pins.sources.imgui "$backend/imgui"
Get-PinnedSource $pins.sources.luajit $lua
Push-Location -LiteralPath "$lua/src"
try { & .\msvcbuild.bat static } finally { Pop-Location }
Push-Location -LiteralPath "$cimgui/generator"
try { & "$lua/src/luajit.exe" generator.lua cl 'internal noimstrv' *> "$outDir/generate.log" } finally { Pop-Location }

$reference = (New-Item -ItemType Directory "$inputs/hexa-binding").FullName
$bindingUrl = "https://raw.githubusercontent.com/HexaEngine/Hexa.NET.ImGui/$($pins.sources.hexaBindingInput.commit)/Generator/cimgui"
foreach ($name in 'definitions.json','structs_and_enums.json') {
    Invoke-WebRequest "$bindingUrl/$name" -OutFile "$reference/$name"
}
python -B "$PSScriptRoot/verify-bindings.py" $reference "$cimgui/generator/output"

$glfw = (New-Item -ItemType Directory "$inputs/glfw").FullName
$glfwHeaders = (New-Item -ItemType Directory "$glfw/include/GLFW").FullName
$sdkInventory = @{}
foreach ($line in (Get-Content -LiteralPath "$SdkRoot/provenance/dependency-inputs.tsv")) {
    $name, $digest = $line -split "`t", 2
    $sdkInventory[$name] = $digest
}
$headerHashes = [ordered]@{}
foreach ($name in 'glfw3.h','glfw3native.h') {
    $path = "external/glfw/include/GLFW/$name"
    Invoke-WebRequest "https://raw.githubusercontent.com/Sakamoto0110/PixieEngine/$EngineSource/$path" -OutFile "$glfwHeaders/$name"
    $digest = Get-Sha256 "$glfwHeaders/$name"
    if ($digest -cne $sdkInventory[$path]) { throw "SDK GLFW header pin mismatch: $name" }
    $headerHashes[$name] = $digest
}
$glfwDll = "$SdkRoot/profiles/authoring/Release/bin/glfw3.dll"
$glfwExports = @(Get-Exports $glfwDll)
if ($glfwExports.Count -lt 100 -or @($glfwExports | Where-Object { $_ -cnotmatch '^glfw[A-Za-z0-9_]+$' }).Count) {
    throw 'Unexpected GLFW exports in the selected SDK.'
}
@('LIBRARY glfw3.dll','EXPORTS') + $glfwExports | Set-Content "$glfw/glfw3.def" -Encoding ascii
& lib.exe /nologo /machine:x64 "/def:$glfw/glfw3.def" "/out:$glfw/glfw3.lib"
@'
add_library(glfw SHARED IMPORTED)
set_target_properties(glfw PROPERTIES
    IMPORTED_IMPLIB "${CMAKE_CURRENT_LIST_DIR}/glfw3.lib"
    INTERFACE_INCLUDE_DIRECTORIES "${CMAKE_CURRENT_LIST_DIR}/include"
    INTERFACE_COMPILE_DEFINITIONS GLFW_DLL)
'@ | Set-Content "$glfw/glfw3Config.cmake"

$env:VCPKG_DISABLE_METRICS = '1'
$env:VCPKG_BINARY_SOURCES = 'clear'
$env:VCPKG_VISUAL_STUDIO_PATH = $vs
$env:VCPKG_DOWNLOADS = (New-Item -ItemType Directory "$inputs/downloads").FullName
$env:VCPKG_MAX_CONCURRENCY = '6'
$brotliSource = "$env:VCPKG_DOWNLOADS/google-brotli-v1.1.0.tar.gz"
Invoke-WebRequest 'https://github.com/google/brotli/archive/v1.1.0.tar.gz' -OutFile $brotliSource
$brotliArchive = Get-FileHash -LiteralPath $brotliSource -Algorithm SHA512
if ($brotliArchive.Hash.ToLowerInvariant() -cne $pins.sources.brotli.archiveSha512) {
    throw 'Brotli source archive pin mismatch.'
}
$dependencyInventory = [ordered]@{}
foreach ($port in 'freetype','vulkan') {
    $triplet = if ($port -eq 'freetype') { 'x64-windows-static-md' } else { 'x64-windows' }
    $manifest = (New-Item -ItemType Directory "$inputs/manifest-$port").FullName
    @{name="pixie-editor-$port"; 'version-string'='1'; dependencies=@($port)} | ConvertTo-Json |
        Set-Content "$manifest/vcpkg.json"
    @{'default-registry'=@{
        kind='git'; repository=$pins.sources.vcpkg.repository; baseline=$pins.sources.vcpkg.commit
    }} | ConvertTo-Json | Set-Content "$manifest/vcpkg-configuration.json"
    try {
        & $vcpkg install "--x-manifest-root=$manifest" "--x-install-root=$outDir/install-$port" "--triplet=$triplet" --clean-after-build *> "$outDir/vcpkg-$port.log"
    } catch {
        Get-Content "$outDir/vcpkg-$port.log" -Tail 35 | Write-Host
        throw
    }
    $dependencyInventory[$port] = @(Get-Content "$outDir/install-$port/vcpkg/status" |
        Where-Object { $_ -match '^(Package|Version|Port-Version|Architecture|Feature):' })
}
if ((Get-Content -Raw "$outDir/install-freetype/vcpkg/status") -cnotmatch
    '(?m)^Package: brotli\r?\nVersion: 1\.1\.0\r?\nPort-Version: 1\r?\n') {
    throw 'Expected the pinned Brotli 1.1.0 port revision 1.'
}

$results = [ordered]@{}
$dlls = [ordered]@{cimgui='cimgui.dll'; base='ImGuiImpl.dll'; glfw='ImGuiImplGLFW.dll'}
foreach ($part in $dlls.Keys) {
    $source = if ($part -eq 'cimgui') { $cimgui } else { $backend }
    $flags = @("-DPIXIE_NATIVE_TARGET=$part","-DPIXIE_NATIVE_SOURCE=$source")
    if ($part -eq 'cimgui') { $flags += "-DCMAKE_PREFIX_PATH=$outDir/install-freetype/x64-windows-static-md" }
    if ($part -eq 'base') { $flags += "-DCMAKE_PREFIX_PATH=$outDir/install-vulkan/x64-windows" }
    if ($part -eq 'glfw') { $flags += "-Dglfw3_DIR=$glfw" }
    try {
        & $cmake -S $PSScriptRoot -B "$outDir/build-$part" -G Ninja "-DCMAKE_MAKE_PROGRAM=$ninja" "-DCMAKE_BUILD_TYPE=$($pins.buildConfiguration)" @flags *> "$outDir/build-$part.log"
        & $cmake --build "$outDir/build-$part" --parallel 6 *>> "$outDir/build-$part.log"
    } catch {
        Get-Content "$outDir/build-$part.log" -Tail 35 | Write-Host
        throw
    }
    $name = $dlls[$part]
    $dll = "$outDir/build-$part/upstream/$name"
    $original = "$BaselineNative/$name"
    $oldExports = @(Get-Exports $original)
    $newExports = @(Get-Exports $dll)
    if (-not $oldExports.Count -or $oldExports.Count -ne $newExports.Count -or
        ($oldExports -join "`n") -cne ($newExports -join "`n")) { throw "Export ABI mismatch: $name" }
    $imports = @(Get-Imports $dll)
    $baselineImports = @(Get-Imports $original)
    if (@($imports | Where-Object { $_ -cnotin $baselineImports }).Count) { throw "New runtime dependency: $name" }
    Copy-Item -LiteralPath $dll -Destination "$binaries/$name"
    $results[$name] = [ordered]@{
        sha256 = Get-Sha256 $dll
        baselineSha256 = Get-Sha256 $original
        exportCount = $newExports.Count
        nativeDynamicImports = $imports
    }
}

$linkMap = "$outDir/build-cimgui/upstream/cimgui.map"
if (-not (Select-String -LiteralPath $linkMap -Pattern 'BrotliDecoderDecompressStream.*brotlidec:' -Quiet)) {
    throw 'The cimgui link map does not identify the static Brotli decoder.'
}
$sourceHashes = [ordered]@{
    generatedCimgui = Get-Sha256 "$cimgui/cimgui.cpp"
    generatedHeader = Get-Sha256 "$cimgui/cimgui.h"
    compatibilityExports = Get-Sha256 "$PSScriptRoot/freetype-exports.cpp"
    cmakeRecipe = Get-Sha256 "$PSScriptRoot/CMakeLists.txt"
    buildScript = Get-Sha256 $PSCommandPath
    bindingVerifier = Get-Sha256 "$PSScriptRoot/verify-bindings.py"
}
$toolchain = [ordered]@{
    msvc = (Get-Item (Get-Command cl.exe).Source).VersionInfo.FileVersion
    msvcSha256 = Get-Sha256 (Get-Command cl.exe).Source
    linkerSha256 = Get-Sha256 (Get-Command link.exe).Source
    windowsSdk = $winSdk
    cmake = (& $cmake --version | Select-Object -First 1)
    ninja = (& $ninja --version)
    vcpkg = (& $vcpkg version | Select-Object -First 1)
    vcpkgSha256 = Get-Sha256 $vcpkg
}
[ordered]@{
    schemaVersion = 1
    pinsSha256 = Get-Sha256 "$PSScriptRoot/pins.json"
    pins = $pins
    sdkCatalogSha256 = Get-Sha256 "$SdkRoot/sdk.json"
    sdkGlfwSha256 = Get-Sha256 $glfwDll
    glfwHeaders = $headerHashes
    toolchain = $toolchain
    sourceHashes = $sourceHashes
    dependencyInventory = $dependencyInventory
    brotliDecoder = [ordered]@{
        librarySha256 = Get-Sha256 "$outDir/install-freetype/x64-windows-static-md/lib/brotlidec.lib"
        linkMapSha256 = Get-Sha256 $linkMap
        staticLinkConfirmed = $true
    }
    binaries = $results
} | ConvertTo-Json -Depth 12 | Set-Content "$outDir/native-build.json" -Encoding utf8
