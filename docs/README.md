# PixieEditor documentation

The [root README](../README.md) is the application entry point. These guides
separate user workflows, implementation contracts and recorded evidence.

The [filesystem map](../FILESYSTEM.md) identifies local ownership and direct
reading routes. The [workspace catalog](../../../.governance/workplaces/void-workplace/docs/catalog/README.md)
routes shared context without copying technical contracts into this repository.

## Use the Editor

| Guide | Question it answers |
|---|---|
| [First-game walkthrough](handbook.md) | How do I save a first project, build C++ code, Play/Stop and export? |
| [Projects and everyday authoring](projects.md) | How do I create/open a project, edit, save, use history and Play/Stop? |
| [C++ component authoring](code-authoring.md) | How do I add code, open it, rebuild and reopen without losing authored changes? |
| [World, Scenes and presentation](world-and-scenes.md) | How do scopes, conversion, World properties, cameras and recovery behave? |
| [Movement and physics authoring](editor-movement.md) | How do presets, colliders, compound bodies, layers and debug views work? |
| [Lighting authoring](lighting-authoring.md) | How do I edit light handles, layer names and debug views? |
| [Field chains and manual locks](editor-field-interactions.md) | Which Inspector modes support interactions, and what are their history/lifetime limits? |
| [Launch options](launch-options.md) | Which explicit SDK, project, layout and scripted-editing inputs are supported? |

| Task | Exact section |
|---|---|
| Create component source and rebuild while carrying authored state forward | [C++ component authoring](code-authoring.md#c-component-authoring) |
| Recover Editor bindings after a candidate World is published | [Session publication and recovery](world-and-scenes.md#session-publication-and-recovery) |
| Work with v3 parent links | [Entity parenting](world-and-scenes.md#entity-parenting) |
| Check the v3 compound conversion limitation | [Compound bodies](editor-movement.md#compound-bodies) |
| Export a saved project and find the log | [Export from the menu](handbook.md#6-export-from-the-menu-and-launch-the-player) |
| Trace a reflected component into the Inspector | [Descriptors and Inspector fields](architecture.md#descriptors-and-inspector-fields) |

## Develop and integrate

| Guide | Responsibility |
|---|---|
| [Architecture](architecture.md) | SDK admission, World replacement, reflected fields, code reopening and export ownership |
| [Development](development.md) | Build against an installed SDK, select profiles, restore packages and prepare a release |
| [Private development channel](development-channel.md) | Local automation, capability boundaries, receipt semantics and qualification inputs |
| [Dependencies](../DEPENDENCIES.md) | Selected Inspector/native dependencies; manifests own exact wiring |
| [Editor project](../src/PixieEditor.csproj) | Build properties, package selection and private/public capability separation |

The Engine owns the public native API and SDK compatibility contract. The Editor
owns UI, session composition, loading, history and project workflows. Its
interop source is supplied by the selected SDK; copying the Engine's API
reference into this repository would create a competing owner.

## Planning, evidence and history

| Route | Contents |
|---|---|
| [Roadmap](../ROADMAP.md) | Editor direction, local outcome admission and the boundary of shared contributions |
| [Accepted work](../TODO.md) | Explicit Editor-local execution and gates; no new native product scope is implied |
| [Documentation policy](governance/documentation-policy.md) | ROADMAP/TODO/optional TASKLIST model, authority and closure rules |
| [Evidence and history](evidence/README.md) | Recorded decisions, qualification limits and locations of earlier Engine-owned records |
| [Extraction origin](extraction-origin.md) | Preserved Engine import commit and original transfer boundary |

At the documentation migration baseline, this repository had no independent
ROADMAP, TODO or TASKLIST. Existing `PIXIE-TODO-054/055/056/060` references identify
work originally coordinated in PixieEngine. The continuation introduces local
ROADMAP and TODO to make Editor contribution ownership explicit; it approves no
separate new Editor-native product scope. The optional [TASKLIST](../TASKLIST.md)
now owns the explicitly transferred Editor contribution tasks.
Historical origin IDs do not determine the current coordination owner. Shared
parent records and pinned nonauthoritative excerpts must identify that owner
without creating a competing scheduler or reactivating completed work.

See [Dependencies](../DEPENDENCIES.md) for the Windows x64 GLFW provider shared with the Engine SDK.
