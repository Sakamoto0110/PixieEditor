# Develop PixieEditor

Open `Pixie.Workspace.slnf` at the Pixie workspace root in Visual Studio.
It references `src/PixieEditor.csproj` through the workspace solution.
Build a standalone checkout with `eng/build.ps1` and an installed SDK.

Build with .NET SDK 10.0.403 and an explicit installed PixieEngine SDK. A public
SDK supplies the normal application bindings. A private SDK and
PixieDevelopmentTools=true enable the qualification application. Use separate
configurations and outputs for the two profiles.

```powershell
./eng/build.ps1 -SdkRoot $publicSdk -CatalogSha256 $publicHash -Locked
./eng/build.ps1 -SdkRoot $privateSdk -CatalogSha256 $privateHash -Configuration Debug -Development -Locked
```

The build script reads the binding pins from the verified SDK catalog. Pass
`-NuGetConfig` to restore from an offline feed and `-PublishDirectory` to
produce a public application artifact.

Pass PixieBuildSdkRoot as an absolute path. Select the trusted catalog hash with
PixieBuildSdkCatalogSha256. Pass the catalog's SHA-256 pins for the binding, props,
metadata and C header using PixieBuildSdkBindingSha256, PixieBuildSdkPropsSha256,
PixieBuildSdkMetadataSha256 and PixieBuildSdkHeaderSha256. The build verifies the
files before compiling. SDK selection at launch is a separate input.

For the Inspector qualification baseline, supply the sealed offline feed and
set PixieInspectorPackageVersion to 0.3.0-pixie021.3. The normal restore uses
PixieInspector.ImGui 0.3.0.

## Rehearse the Windows x64 release

Supply the Engine workflow artifact directory containing `release.json` and
the SDK ZIP. Obtain the trusted ZIP and catalog hashes from the approved
Engine run. Supply the approved Inspector artifact with both
`0.3.0-pixie021.3` packages at its root. Choose a new output directory.

```powershell
python -B eng/rehearse-release.py `
    --engine-assets $engineAssets `
    --sdk-archive-sha256 $archiveHash `
    --sdk-catalog-sha256 $catalogHash `
    --inspector-feed $inspectorFeed `
    --out $candidate
```

The rehearsal verifies both hashes, the SDK inventory and the selected
Inspector packages. It restores into a fresh locked package cache and writes
the Windows x64 ZIP, receipt and checksums under `assets/`. Pass
`--upstream-feed` for an offline mirror of the remaining locked packages.
The [native provenance manifest](../eng/native/win-x64/release-provenance.json)
records the admitted DLLs, verified notice inputs and outstanding evidence.
The packaged Hexa backend imports the Vulkan loader even for an OpenGL Editor
session. The candidate also requires .NET Runtime 10 x64.

The Windows workflow runs the same command. Select the Engine and Inspector
repositories, approved run IDs and artifact names, and both SDK hashes.
Configure `PIXIE_SDK_ARTIFACT_TOKEN` for read access to both sets of workflow
artifacts. The verification artifact is retained for 30 days.

## Local Editor data and test inputs

The dock layout lives under the PixieEditor application-data directory.
Use --import-layout with an absolute legacy file path to import it when no
current layout exists. Development tests can supply an isolated --layout path.
The capture runner accepts the private SDK and explicit project or module
artifact. Legacy source fixtures must supply PIXIE_TEST_ENGINE_FIXTURE_ROOT.
