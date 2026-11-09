# PixieEditor documentation

The [root README](../README.md) is the application entry point. These guides
separate user workflows, implementation contracts and recorded evidence.

## Use the Editor

| Guide | Question it answers |
|---|---|
| [First-game walkthrough](handbook.md) | How do I complete the first project/code/play/export loop through Computer Use? |
| [Projects and everyday authoring](projects.md) | How do I create/open a project, edit, save, use history and Play/Stop? |
| [C++ component authoring](code-authoring.md) | How do I add code, open it, rebuild and reopen without losing authored changes? |
| [World, Scenes and presentation](world-and-scenes.md) | How do scopes, conversion, World properties, cameras and recovery behave? |
| [Movement and physics authoring](editor-movement.md) | How do presets, colliders, compound bodies, layers and debug views work? |
| [Lighting authoring](lighting-authoring.md) | How do I edit light handles, layer names and debug views? |
| [Field chains and manual locks](editor-field-interactions.md) | Which Inspector modes support interactions, and what are their history/lifetime limits? |
| [Launch options](launch-options.md) | Which explicit SDK, project, layout and scripted-editing inputs are supported? |

## Develop and integrate

| Guide | Responsibility |
|---|---|
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
| [Documentation policy](governance/documentation-policy.md) | ROADMAP/TODO/optional TASKLIST model, authority and closure rules |
| [Evidence and history](evidence/README.md) | Recorded decisions, qualification limits and locations of earlier Engine-owned records |
| [Extraction origin](extraction-origin.md) | Preserved Engine import commit and original transfer boundary |

At the documentation migration baseline, this repository had no independent
ROADMAP, TODO or TASKLIST. Existing `PIXIE-TODO-054/055/056/060` references identify
work originally coordinated in PixieEngine. They are not duplicated as a new
Editor queue or reactivated by moving the guides. Create local planning files
when accepted Editor work needs them, with explicit ownership for shared scope.

D09 remains frozen under the existing [policy](governance/documentation-policy.md#evidence-and-boundaries).
