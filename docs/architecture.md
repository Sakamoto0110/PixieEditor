# PixieEditor architecture

PixieEditor owns the authoring UI, project routing, selection and history.
The selected Engine SDK supplies the native session, World, reflection
descriptors, rendering and exporter. PixieInspector supplies the field views.

## Project admission and World replacement

    SDK selection -> EngineSession -> native project/session
    Project manifest + scene + optional module -> candidate World -> publication
    Published World -> new live handles -> rebind selection and Inspector

The public Editor selects an installed authoring SDK before opening a project.
It checks the catalog, selected profile and project requirements. The native
session loads authored content and prepares the World and its services.
Play and Stop can replace the World. The Editor tracks authored entities by
`SceneId` and resolves new handles after replacement. UI bindings must be
rebuilt when native storage changes. An authored file Save, an in-memory
history entry and a Play snapshot serve different lifetimes.

For whole-document edits, the coordinator rechecks session/content/storage/history
identity, publishes a prepared candidate and rebuilds bindings. If rebinding
fails after publication, choose **Retry Inspector recovery** to rebind the
committed World. The recorded edit remains applied.

[Projects and everyday authoring](projects.md#edit-play-and-save) ·
[Session publication and recovery](world-and-scenes.md#session-publication-and-recovery) ·
[Native edit and play](https://github.com/Sakamoto0110/PixieEngine/blob/ac48e01ed040ee607662e6cdf1f4ff9742c4b0f4/docs/modules/runtime/REFERENCE.md#edit-and-play)

## Descriptors and Inspector fields

    Built-ins / Project::Describe -> native component descriptors -> C API
       -> EngineSession -> InspectorFactory -> PixieInspector views
       -> ComponentHandle -> native field read/write

Ordinary Inspector sections show component types shared by the selected
entities. The separate **Add component** menu lists registry entries for
possible addition. If admission rejects a listed type for the current selection,
the entry is disabled and hovering over it shows the reason. Descriptor kinds,
labels and options shape the field controls. Field writes go back to the live
World through the session. The Editor coordinates edit history and advances
`EngineSession.BindingRevision` when replacement succeeds. Old ComponentHandle
instances reject access once their bindings expire. A game's module can describe a new component
without adding a C# Inspector class.

[Native reflection](https://github.com/Sakamoto0110/PixieEngine/blob/ac48e01ed040ee607662e6cdf1f4ff9742c4b0f4/docs/modules/runtime/REFERENCE.md#reflection) ·
[Field interactions](editor-field-interactions.md#history-and-lifetime) ·
[InspectorFactory](../src/Inspection/InspectorFactory.cs) ·
[ComponentHandle](../src/Inspection/ComponentHandle.cs)

## Code creation and reopening

The Editor generates or updates the project files and registry from the SDK
templates. The build/reopen handoff releases the old module, builds and opens
the project in a new process. It can restore unsaved authored content and
selection by stable ID. Undo/Redo and mutable Project state start fresh.
A pending attachment can add a new entry after reopening. Use **Open code** to launch VS Code for the declared project source.

[Build and reopen](code-authoring.md#build-and-reopen) ·
[Open code in VS Code](code-authoring.md#open-code-in-vs-code) ·
[Scene versions and parenting](world-and-scenes.md#entity-parenting)

## Viewport and export

The Editor owns the window, GL context, viewport tools and focus routing.
The native session draws into the embedded surface. Editor code coordinates
native resource disposal before the host releases the context.

    Saved project + selected SDK -> ExportPanel -> ProjectExportJob
                                 -> native exporter -> paired shipping player

In Editing, use **File > Export game...** after saving. The job invokes the
selected SDK's exporter with `--sdk` and supplies a separate shipping work
directory for a code project. The job mirrors stdout and stderr to the process
console and retains the latest 512 lines in **Build log**. The Engine owns shipping compilation, asset staging and
verification. Launch the resulting player separately to observe the export.

[Export from the menu](handbook.md#6-export-from-the-menu-and-launch-the-player) ·
[Exporter commands](https://github.com/Sakamoto0110/PixieEngine/blob/ac48e01ed040ee607662e6cdf1f4ff9742c4b0f4/docs/handbook/export-and-troubleshooting.md#optional-diagnostic-command-line) ·
[ExportPanel](../src/Development/ExportPanel.cs) ·
[ProjectExportJob](../src/Development/ProjectExportJob.cs)

## Contributor entry points

Use the [filesystem map](../FILESYSTEM.md) for code ownership and existing
checks. The [evidence index](evidence/README.md) routes observations to their
recorded baselines.
