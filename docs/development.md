# Develop PixieEditor

[Documentation index](README.md) · [Launch options](launch-options.md)

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

For the Editor-to-SDK boundaries in a live session, see the
[architecture guide](architecture.md#project-admission-and-world-replacement).

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
Use `--rebuild-native` to compile the Windows x64 ImGui DLLs from the checked-in
[source pins](../eng/native/win-x64/rebuild/pins.json). The recipe requires the
recorded MSVC and Windows SDK versions, Visual Studio's vcpkg, CMake, Ninja
and network access to the pinned sources. It checks generated binding layouts,
native exports and runtime imports before staging the replacements. The
candidate includes `native-build.json` with the toolchain identities, source
and DLL hashes, and the static Brotli decoder evidence.
The packaged Hexa backend imports the Vulkan loader even for an OpenGL Editor
session. The candidate also requires .NET Runtime 10 x64.

The Windows workflow runs the same command. Select the Engine and Inspector
repositories, approved run IDs and artifact names, and both SDK hashes.
Configure `PIXIE_SDK_ARTIFACT_TOKEN` for read access to both sets of workflow
artifacts when using cross-repository downloads. A same-repository Inspector
artifact uses the workflow token. If an existing public Engine release mirrors
the approved run, supply its tag and the trusted run receipt SHA-256. The
workflow checks that receipt and still requires the approved SDK ZIP and
catalog SHA-256 values. The verification artifact is retained for 30 days.

## Local Editor data and test inputs

The dock layout lives under the PixieEditor application-data directory.
Use --import-layout with an absolute legacy file path to import it when no
current layout exists. Development tests can supply an isolated --layout path.
The capture runner accepts the private SDK and explicit project or module
artifact. Legacy source fixtures must supply PIXIE_TEST_ENGINE_FIXTURE_ROOT.

## Application assets

The editor's icon, on its executable and its window, is the neko mark of the shared asset store
(`vdr` alias `neko-purple-light`), copied into `src/assets/` with `vdr cp` under its curated
name. The window takes its 32-bit frames as pixels through GLFW, so it shows on every host.

Exact managed/native dependency selections belong to [DEPENDENCIES.md](../DEPENDENCIES.md)
and the project/build manifests; this guide does not maintain a second version list.

## Documentation governance

Use the [documentation policy](governance/documentation-policy.md) for planning,
evidence ownership and roadmap closure.

## Void Agent Workplace

The workspace owns project profiles outside the VAW source repository. The
active Pixie family contains Engine, Editor, Inspector and Lib at profile
revision 4; product code and local project contracts remain independently owned.
The Editor's `vaw-local` remote is a local binding identity, not publication.

Use the activated workspace launcher from the project root in PowerShell 7:

```powershell
& ../../.governance/vaw.ps1 -Action resolve-project-binding -Arguments @{ RepositoryPath = (Get-Location).Path }
& ../../.governance/vaw.ps1 -Action resolve-workplace-binding -Arguments @{ WorkplacePath = (Resolve-Path ..).Path }
```

The launcher selects external profiles, the explicit registry, and the installed
package set. Require `bound` for the project and `workplace_bound` for the family.
Its activation receipt records the platform location, exact pins, packages and
shared-policy hash. The shared policy is materialized from the workspace's
versioned configuration; runtime copies and coordination records are local.

Do not use an old VAW checkout's default profile directory after rebind. A
binding grants no publication authority. Standalone project build and validation
still work without VAW. Never run `git clean -x` on the enclosing VAW checkout:
its ignored `projects/` contains independent repositories and unpushed work.
