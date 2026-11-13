# Projects and everyday authoring

[Documentation index](README.md)

## Project folders

A project is a folder with one `<Name>.pixie` file and its scenes
(runtime reference (PixieEngine docs/modules/runtime/REFERENCE.md#project-folders)). It holds no path to
the engine or to anything outside it, so it can be copied, versioned, or opened on another machine
as it is. The editor creates no version-control files.

## Create and open

- **Start screen.** Without `--game` or `--project`, the editor opens on New project, Open project,
  and the recent projects (a click opens one; a missing one is marked, with Forget).
  - **New project** takes a location, chosen in a folder picker drawn in ImGui (Up, the drives,
    the folders inside, and a path to type and confirm with Enter), and a name; it makes the folder
    `<location>/<name>`, which must not exist or be empty, and says why before Create can be
    pressed. The project takes the folder's name, and its scene starts with one shape.
    The picker also offers **New folder**: enter a name and choose **Create folder** (or press
    Enter) to create and enter a child folder at the current location; **Cancel** leaves it alone.
    Existing names are refused, and filesystem errors are shown in the picker. The same control
    is available in the Open project and asset pickers.
  - **Open project** takes a folder that holds a `.pixie` file; project folders are marked in the
    picker, and Open waits until the current folder is one.
- A project runs on the shared engine's generic scene project, so it opens without a
  compiler. The recent projects and the place of the last one are kept in the user's settings
  (`PixieEditor/settings.json` under the application data folder), never in a project.

## Edit, play and save

- **Edit and play.** A project opens in edit mode: time stands still, the time bar and Game wait,
  and the mouse selects. Edits change the World; **Save** (Ctrl + S, or File) writes the scene, and
  the window's title marks unsaved changes with `*`. **Play** builds the World again from the scene
  and runs it, with the mouse on Game input; while playing, the title and viewport controls say
  Playing, and the viewport is framed in orange. The status tooltip explains that runtime changes
  are discarded. **Stop** goes back to the
  scene as it was at Play. Closing the window, or File, New or open project (a new editor on the
  start screen), with unsaved changes asks first: Save, Don't save, or Cancel.
- **Code.** A project can have C++ code of its own. **Code, Add code** writes `CMakeLists.txt` and
  `src/<Id>Project.{hpp,cpp}` into the project from the engine's templates, with an example
  Behaviour, `Spinner`, then restarts the editor on the project. Opening a project that has code
  builds its game module against the selected installed SDK. The build screen
  shows CMake output from the Editor-owned temporary build cache. CMake is
  taken from PATH or Visual Studio. On failure, **Try again** and **Open without
  code** are available. The latter opens with the SDK's built-in components
  and preserves unknown component data. **Code, Build and reopen** waits for
  the old Editor process to release its module, then rebuilds it and restores
  the authored session. Use **Export game** to build a shipping player.
- **Authoring.** **+ Create > Basic shape**, in the Entities panel, adds an entity named `entity <n>` to the
  scene, with a transform and a 24-unit circle, and selects it. In the inspector, Add component
  lists the types the selected entities do not all have, and a component's header has a close
  button that removes it from all of them. The inspector's first line is the selected entity's
  name, which can be typed over; Enter, or leaving the field, renames it. **Duplicate** (Ctrl + D,
  the Edit menu, or the viewport's menu) copies the selected scene entities 16 units to the side
  and selects the copies; references between the copies follow them, and the rest keep their
  targets. **Delete** (the key, or the menus) deletes the selection. The keys act unless a text
  field has the keyboard.
- **Sprites.** PNG files dropped on the editor's window become sprites, one per file, where the
  mouse is over the viewport (in the middle of the view when it is elsewhere), each 16 units to the
  side of the last, named after its file, selected, and as one step; other files are left out, and
  the status line says so. **New sprite here...**, in the viewport's menu on empty space, picks a
  PNG and places it at the click. A sprite is drawn at one world unit per image pixel; one that
  would be larger than half the view is given a smaller scale. An image from outside the project
  is copied into the project's `images/` folder first (an identical one already there is used
  instead of a second copy, and another file of the same name gets a number), and the status line
  names what was copied, so the project stays whole on another machine. The image of an open
  project's sprite that is saved again by a painting program shows at once.
- **Project assets.** Open **View > Asset Manager** to browse project PNG images, TrueType fonts
  and sprite atlases. Search by project-relative path, filter by type, and select a row to preview a
  PNG or copy its path. Select a `.atlas.json` descriptor to search frames by name or ID and preview
  the selected frame. Use **Refresh** after adding, repacking, moving, or removing files outside the
  Editor.
  In Edit mode, select scene entities with a Sprite component and click **Assign frame to selected
  sprites**. The assignment is one undo step and saves the descriptor path and stable frame ID.
  In the Sprite inspector, **Show atlas frame** opens that reference in the Asset Manager and shows
  a diagnostic if its frame or assets are missing. **Use direct PNG** clears the atlas reference and
  restores the sprite's authored PNG, region, and pivot. Choosing a PNG in the inspector also
  switches the source. Atlas assignment requires an Engine SDK with sprite atlas support.
  **Import...** accepts a PNG, a TTF, or a folder. Folder imports walk subfolders and preserve the
  selected folder's name and subfolder paths under `images/` and `fonts/`. Unsupported files,
  including ZIP archives, are skipped. Extract an archive outside the project first to import its
  contents as a folder. Imports leave the source files in place, reuse identical destinations,
  and number conflicting file names. Importing files leaves scene entities and references intact.
- **Undo and redo.** Ctrl + Z undoes, Ctrl + Y or Ctrl + Shift + Z redo, and the Edit menu names
  what they would undo or redo. Every change to the scene in edit mode is one step: an inspector
  edit (a scrub, a drag on a slider, or a typed value, from press to release, over every selected
  entity), a move, creating, deleting, duplicating, adding or removing a component, and renaming.
  Steps name entities by scene ID, so they hold after the World is built again. The history lasts
  the editor session, through Play, Stop, and Save; changes made while playing are not steps, since
  Stop discards them, and undo and redo wait for Stop. Entities the game's code generates are not
  in the scene, and not in the history. Each step is logged.

## Viewport and panels

- **Viewport.** The engine draws the World into a texture of the editor's own OpenGL 4.5 core
  context (`pixie_session_render`); the window shows it with `ImGui.Image`, flipped, since GL's
  first row is the bottom. The keys go to the game while the viewport has focus (click it first).
  The game steps at its fixed rate with the editor's wall time. Two buttons at the top choose what
  the mouse does:
  - **Game**: the mouse over the image goes to the game, in the image's pixels, as in its player.
  - **Select**: a click on a boid, an attractor, or a spawner selects it; Shift + click adds it to
    the selection or takes it out; a click on empty space clears the selection. A drag that starts
    on an entity moves it, and the whole selection with it when it was selected (otherwise it is
    selected first); Shift held during the drag keeps it to the axis it moved most along; with
    **Snap** on the toolbar, the dragged entity lands on a grid of 8, 16, 32, or 64 world units and
    the others keep their places around it. Each drag is one step of the history. A drag that
    starts on empty space selects every entity whose center is in the box (Shift keeps the rest).
    A right click opens a menu of what can be done at that point. On an entity, it joins the
    selection if it was not in it, and the menu duplicates or deletes the selection. On empty space, the menu offers Create entity here (a scene
    entity with a circle, at that point in world units); for a game, Ask the game to create one
    here (a boid heading somewhere at random in boids and flocks, a particle at rest in particles);
    and, where a particle system exists, Delete the particle here (the nearest within 6 pixels);
    and, with a Sprite component, New sprite here... (see Sprites).
    What the menu does is logged. Particles are never selected. The middle button and the wheel still go to the
    game, to pan and zoom; otherwise the game sees no pointer, so boids do not chase the cursor
    while you pick them. Selected entities are outlined: a sprite by its rectangle, turned as it is
    drawn, a text by its block of lines, a path along its line with its ends circled, anything else
    by a circle. What is drawn on
    top is picked first: shapes, then sprites, then paths, then sprites of negative layers.
- **Entities.** Every entity, oldest first, by name or number; only the rows in view ask the engine
  for a name. A click selects one, Ctrl + click adds or takes out one, and Shift + click selects the
  range from the last one clicked. **+ Create** groups Basic shape and the existing physics presets;
  hover a preset to read its purpose. **Character motor starter** still requires project Behaviour
  code for movement. **Physics settings** appears separately under Settings. Total and selected
  counts remain visible above the list.
- **Inspector.** For the selected entities, one section per component that all of them have: a PixieInspector built from the
  engine's field descriptors (`src/Inspection/InspectorFactory.cs`), with one `AddField` per field (two
  for a point or a vector, four for a rectangle) over a handle of the component, and the label,
  tooltip, range, scrub, unit, and editor the descriptor carries. A color field edits with
  PixieInspector's color editor: its four channels, and a picker on its swatch. An entity field (a particle
  spawner's system or target) is a choice among the live entities that have the component it
  references, shown as "name #index", or "(none)". With several entities selected, the inspector
  is bound to the first and to the rest with `AddBind`, so an edit changes all of them; a field
  whose values differ shows "--", except a number that scrubs, which shows the first entity's value
  so a scrub moves each from its own. Every mixed row's label is amber, and its tooltip says the
  entities differ. The fields read and write the
  component through the C API on every frame; the native World is the only copy of the state. No
  class per component exists in the editor: a component added to a game's library shows up without
  rebuilding it. A text field, such as a sprite's `image`, is drawn above the rest of its section: a
  text box whose edit is one step when it lets go of the keyboard, "(mixed)" in its label when the
  selected entities differ, and, for a field that names a file (an asset), a **...** button that picks
  one of its kind: a PNG for an image, starting in the project's `images/` folder, a `.ttf` for a
  font, starting in `fonts/`, each copied into that folder from outside the project (an identical
  copy reused, a taken name numbered). Under an image, a preview with its size, or, in red, why it
  cannot be read (a missing file, not a PNG); under a font, in red, a file that does not exist. Adding
  a `Text` component gives it the project's first `.ttf` (in `fonts/` first), so it shows at once. A list of points, such as a path's `points`, is drawn there too: its count, a row of X and Y
  per point, typed over and written as one step when the row lets go of the keyboard, a **-** button
  per row that removes the point, and **Add point**, which adds one 32 units right of the last; each
  is one step. When the selected entities' lists differ, the field asks to select one.
- **Viewport controls (layout prototype).** Playback and tools occupy separate rows inside the
  Viewport dock, above its rendered image. The playback row contains Play/Stop, Pause/Resume,
  Step, time scale and current mode. The tools row contains Select/Game input, collider handles
  and snapping. Controls wrap at narrow widths; the image and picking origin begin below them.
  File, Edit and Code remain in the application menu. Existing saved docking layouts are retained.
  The bounded visual prototype was accepted on 2026-11-02; see the
  [recorded acceptance](evidence/authoring-decisions-2026-11-06.md#viewport-session-and-entities).
  The wider PIXIE-TASK-231 scope retains its separate disposition.
- **Time.** The viewport playback row slows time down, pauses or resumes, and speeds it up: 0.1, 0.25, 0.5,
  0.75, 1, 1.25, 1.5, 1.75, and 2 times. The fixed step stays 1/60 s; faster runs more steps per
  frame, so a heavy World (5,000 flocking boids) falls a little behind at 2 times.
- **Scenario.** Each game's starting values (counts, attractors) live in a scenario component on
  the `scenario` entity; its inspector has a "Rebuild the scene" button that builds the World again
  from the edited values.
- **Session.** The project or game name, Editing/Playing/Paused mode, scene save state and World's
  time appear first. Code status distinguishes the built-in runtime from a loaded project module.
  **Reset scene** resets a project from the scene's current entities; **Reset game** resets a game.
  Neither action recompiles C++ code. Statistics (entity count, steps, step cost and editor fps)
  and Project details (folder and library) are collapsible. Physics and Lighting debug remain here,
  as do buttons that open the other games built next to this one in a new editor
  (P/Invoke binds the library once, so one process hosts one game or project).
