# First game: Editor walkthrough

This guide accompanies the PixieEngine **consumer handbook**. It owns the UI
instructions; the Engine handbook owns C++ API usage. Read its setup page first.
Use a compatible public Editor, a complete installed SDK and a separate writable
game folder. You do not need an Engine source checkout to author a game.

## Required workflow for the Claude handoff

**Computer Use is mandatory for Editor interactions.** Create/open the project,
edit Inspector values, attach components, save, undo/redo, Play/Pause/Stop,
generate code/components, Build and reopen, and Export through the actual UI.
Do not substitute command-line scene generation, private automation, MCP scene
mutations or direct edits of scene files and then describe that as UI usage.

The Editor does not contain a C++ text editor. Use an allowed external code
editor/file tool for the game's user-owned C++ files, then return to the Editor
and build through its menu. This division is part of the workflow. If all code
entry must also be mouse/keyboard driven, use an external text editor through
Computer Use; PixieEditor alone cannot supply that editing surface.

Observe each action's result before the next action. Coordinates vary with
window size, panel layout and DPI. Native ImGui content may not expose individual
accessibility elements, so use fresh screenshots and visible labels rather than
guessing controls. If Computer Use is unavailable or interrupted, report it;
do not silently substitute another authoring path.

Use the launch route supported by your Computer Use tool: direct application
launch if available, or open the supplied folder in an approved file browser
and double-click the executable. Some tools grant access only after an app is
running. State any preparatory shell launch separately from UI authoring; do
not bypass the tool's access restrictions or assume another agent has yours.

For missed native UI clicks, move the pointer to the control first, allow the
UI to process the hover, then click and observe. A held control requires a real
mouse-down/mouse-up sequence if the tool supports it; repeated clicks are not
a hold. Use fresh screenshots after rebuilds, window moves and layout changes.
There is no universal delay that guarantees input delivery.

## 1. Choose the SDK and create a project

1. Launch `PixieEditor.exe` from the complete application folder supplied by the
   maintainer. Keep its dependencies beside it.
2. On the start screen, inspect the SDK summary. Click **Choose SDK** if needed.
3. Navigate to the installed SDK root containing `sdk.json`. In the folder
   picker, click the address/path field, replace its text with the supplied root,
   press Enter and click **Use SDK**. Expect the version, authoring profile and
   native configuration in the summary. It is normal for installation paths to
   be machine-specific; do not write them into portable game files.
   If a path works in an assistant's file tools but not here, use the verified
   physical path and identity supplied in the handoff. File-tool access alone
   does not prove access from the Editor's process.
4. Click **New project**. Choose the parent directory in **Location**, and enter
   a distinct **Name** such as `HandbookPractice`. The resulting location/name
   directory must be new or empty. Click **Create**.
5. Expect the scene to open in **Editing**, with a starter blue circle named
   `shape`. The Session panel reports the project, Saved state and runtime.

For an existing game use **Open project** or its recent entry. Select its folder
or `.pixie` manifest as the picker permits. The current project is not overwritten
by choosing New. Prompts about unsaved changes have Save, Don't save and Cancel;
make that choice deliberately. See [projects](projects.md) for detailed routing.

## 2. Edit, undo, redo and save

1. Click `shape` in **Entities** or click the visible shape in the viewport.
2. In **Inspector > Transform2D**, click **position x**, enter `100`, and press
   Enter. Expect the object to move right and the title to acquire `*`.
3. Press **Ctrl+Z**. Expect x to return to its previous value. Press **Ctrl+Y**
   and expect the new value again. Finish text entry first; a focused text edit
   can have its own key handling.
4. In **Shape > kind**, choose **Triangle** so later rotation is visible.
5. Press **Ctrl+S**. Expect the `*` to disappear and Session to report Saved.

Position and size use world units; +Y points down. The Inspector displays
rotation in degrees although C++ uses radians. Shape size is half width/height.
Numeric fields can be typed; do not depend on viewport dragging for precision.
**+ Create** creates scene entities/presets. **Add component** attaches a type to
the selected entity; it is not another entity. An `x` on a component header
removes that component. Do not remove data just to dismiss a panel.

The padlock/chain icons are field interaction modes, not game mechanics. See
[field interactions](editor-field-interactions.md) before relying on their
selection/history behavior. Keep the ordinary v1 format for the C++ Behaviour
tutorial; **Convert to v2** is a separate explicit migration, not a required
setup step.

A consumer reported stale Inspector text after Undo of a custom field even
though the game value reverted. If encountered, finish the edit, reselect and
recapture, and compare the scene/game state and Edit menu history before making
another edit. This is a reported observation, not a defect reproduced by this
guide's author.

## 3. Add code and run a small behavior

1. Choose **Code > Add code**. It generates a CMake project, Project class,
   component registry and Spinner example, then rebuilds and reopens.
2. Allow the Editor to close/restart. Rediscover its new window; the old window
   ID is no longer an input target. Wait for either the project or a diagnostic.
3. Confirm Session's Code field now names `<Id>-module.dll` and your selected
   entity/pose survived.
4. Select the triangle, click **Add component**, and choose **Spinner**.
5. Inspect Spinner's **speed**. Its UI unit is degrees per second; the generated
   default is approximately 85.94 degrees/s (1.5 radians/s).
6. Click **Play**. Expect simulation time to advance and the triangle to rotate.
7. Click **Pause**; time and rotation stop. Click **Step**; one simulation tick
   advances. **Resume** continues. **Stop** restores the authored state captured
   when Play began, including unsaved authored changes that existed at that time.
8. Save in Editing. A scene changed while playing is not a way to author a
   permanent pose. Reset scene is also a reconstruction action, not Save.

Keyboard/mouse gameplay needs **Game input** in the viewport and the appropriate
focus/hover; the host cancels held controls on focus loss or blocking modals.
**Select** is for selection/editing. Confirm the active mode visually after Play.

Test a key the game actually binds. HUD direction buttons do not automatically
bind keyboard arrows. In the consumer follow-up, the user manually confirmed
Space in the Editor viewport; unsuccessful synthetic keypresses did not
establish an Editor keyboard defect. If automation produces no response, check
the foreground window, Game input, viewport focus and component enablement,
then record the injection method and manual comparison separately. The same
test must be observed separately in an exported player to claim player coverage.

## 4. Create and edit your own component

1. Select the intended entity in Editing, then **Add component**.
2. At the bottom, under **Create a C++ component**, enter `PlayerController`
   into **Name** and click **Create component and rebuild**.
3. Wait for reopening. The new component should be attached to the original
   selection. Creation writes reusable source and registry entries; other
   entities can later attach the same type.
4. Use its **Open code** button or open the game folder in an external code editor.
   Edit `src/components/PlayerController.hpp` and `.cpp` using the complete
   Engine handbook example. Do not edit generated registry files/hashes or
   template-owned CMake text. Save the source files in that editor.
5. Choose **Code > Build and reopen**. Wait and inspect errors, or confirm the
   component now exposes the example's **enabled** and **speed** fields.
6. Attach **Velocity2D** and **KeyboardControl** through Add component. Use the
   generic KeyboardControl, not `KeyboardControl2D (legacy)`. Remove Spinner
   if you want a nonrotating movement example. Save.
7. Play, choose Game input and use WASD in the viewport. Observe movement, release
   to stop, then Stop and check the authored pose is restored.

The build/reopen handoff can preserve unsaved authored content and selection;
Undo history and arbitrary mutable C++ Project state do not persist. Save before
routine rebuilds for an easy recovery point. If checking unsaved preservation,
make a small Inspector edit, build without saving and verify its value and `*`
after reopening. A successful compile without that observation is not evidence
of preservation. See [code authoring](code-authoring.md) for recovery details.

## 5. Save and reopen

1. Stop and press Ctrl+S. Verify Saved.
2. Use **File > New or open project...** to return to the start screen.
3. Reopen the practice project from recent projects (or Open project).
4. Verify its authored entity, pose, component types and configured values.
5. Play/Stop again to distinguish serialized content from an old in-memory session.

For an actual game also check assets, scene selection, camera and game-owned save
data as applicable. The Editor's scene save does not serialize all runtime memory.

## 6. Export from the menu and launch the player

1. Stop, save scene/source and finish any needed Build and reopen.
2. Choose **File > Export game...**. Select a new/empty output folder outside
   the project, such as `C:\Games\Exports\HandbookPractice-r1`.
3. Click the export action and wait for the panel's result/log. Preserve any
   failure text. Do not repeatedly merge partial attempts into the same folder.
4. Open the successful output folder and launch its game executable. Keep all
   staged files together. Verify visible content and input in the standalone
   player; do not assume the Editor result establishes the player result.
5. Close the player normally. Keep the project and export paths in the report.

If the export log opens clipped at the window edge, resize/reposition it when
possible. Otherwise state that the log was unread and use the produced output,
selected runtime identity and actual player launch as separate evidence. Do
not report a success message you could not see. Before distribution, check that
asset license/notice files accompanied the exported assets; discovery of a font
does not automatically include the text file next to it.

This is a local export, not publication. See the Engine handbook's export page
for paired-runtime rules and code-only asset limits.

## Continue from the first success

| Game need | UI route | Code/contract route |
|---|---|---|
| More objects | + Create, Inspector | Game-owned components and Entity lifetime |
| Per-object tuning | Generated component, reflected Inspector fields | Describe/Field; transient vs persisted state |
| Colliders/motors | Presets, Add component, Physics debug | [Movement guide](editor-movement.md), scene physics services |
| Lights/indicators | Add component, Lighting debug | [Lighting guide](lighting-authoring.md), Engine drawing chapter |
| Clickable HUD | External C++ and Build/reopen | ScreenCanvas helper; game-owned hit testing |
| Collision particles | Configure pools/effects, inspect Play | Read physics events, explicitly Emit |

Save a `CONSUMER-REPORT.md` in the game folder using the accompanying Engine
handbook's report template. Separate UI actions from external C++/asset edits,
direct observations from source review or user reports, and identify each build.
Include changed SDK selections and any manual export additions.

Report what was observed and what remains unverified. The accompanying Engine
handbook validation record identifies the local walkthrough actually executed;
this guide does not claim every feature in this table has been visually tested.
