# PixieEditor

An independent C#/.NET 10 editor for PixieEngine, using Dear ImGui and
[PixieInspector](https://github.com/Sakamoto0110/PixieInspector). It creates
projects, edits scenes and C++ components, and exports games through an
explicitly selected installed Engine SDK. The native viewport uses OpenGL 4.5 core.

**Making your first game? Follow the [Editor walkthrough](docs/handbook.md)**
alongside the PixieEngine consumer handbook supplied with your SDK documentation.

The Editor owns its source, build, dependencies and version. An Engine source
checkout or a particular sibling-folder layout is not a normal build/runtime
input. Start with the [documentation index](docs/README.md) for guides,
contracts, recorded evidence and planning ownership.

Workspace contributors can use the [Editor filesystem map](FILESYSTEM.md),
[local roadmap](ROADMAP.md) and [accepted work](TODO.md) to find the Editor's
contribution boundary. The [workspace entry](../../.governance/workplaces/void-workplace/README.md)
routes shared governance and coordination; those documentation routes do not
become build or runtime dependencies.

## Build and run

Open `Pixie.Workspace.slnf` when working in the Pixie workspace, or build this
checkout with the installed SDK and its trusted catalog hash:

```powershell
./eng/build.ps1 -SdkRoot $installedSdk -CatalogSha256 $trustedCatalogSha256
```

Launch the built or packaged `PixieEditor.exe`. Select an installed SDK in the
Editor, then create or open a project. A project without C++ code needs no
compiler. The SDK used to build the Editor and the SDK selected at launch are
separate inputs; compatibility is checked explicitly. See
[development](docs/development.md) for prerequisites, profiles and release preparation.

## Projects

[Projects and everyday authoring](docs/projects.md): creation, opening, editing,
Play/Stop, saving, history, selection, assets and the Inspector.

## Options

[Launch options](docs/launch-options.md): SDK overrides, project/module selection,
layout import and scripted editing. Normal project use starts in the UI.

## Local development MCP channel

[Private development channel](docs/development-channel.md): explicit opt-in,
capabilities, mutation receipts and the existing qualification recipe.

## World properties

[World properties and session recovery](docs/world-and-scenes.md#world-properties).

## Scene authoring

[Scene scopes, membership and explicit conversion](docs/world-and-scenes.md#scene-authoring).

## Collision and movement authoring

[Movement, collider handles and physics diagnostics](docs/editor-movement.md).
[Lighting handles and debug views](docs/lighting-authoring.md).

## Generic entity input and C++ component authoring

[C++ workflow](docs/code-authoring.md): creating components, opening source,
building/reopening and preserving authored state.

## Canonical presentation and authored cameras

[Presentation, Environment and camera selection](docs/world-and-scenes.md#canonical-presentation-and-authored-cameras).

## Project records

[Dependencies](DEPENDENCIES.md), [extraction origin](docs/extraction-origin.md),
[evidence and history](docs/evidence/README.md),
[documentation policy](docs/governance/documentation-policy.md) and [license](LICENSE).
