# Launch and scripted editing options

[Documentation index](README.md)

Normal project use starts in the UI. These optional arguments also support
explicit launch/restart inputs and scripted workflows. The parser in
[`src/Program.cs`](../src/Program.cs) owns accepted syntax. Command recipes
document behavior; they do not record a new test execution.

| Option | Default | Meaning |
|---|---|---|
| `--sdk <folder>` | Editor selection | Installed SDK override; project files do not store this machine-local path |
| `--sdk-profile <profile>` | `authoring` | Select `authoring` or the explicitly admitted private `local-development` profile |
| `--native-configuration <Debug\|Release>` | `Release` | Select the native SDK configuration |
| `--sdk-id <sha256>` | none | Expected SDK identity across an Editor restart |
| `--sdk-build-id <sha256>` | none | Expected profile/build identity across an Editor restart |
| `--sdk-manifest-sha256 <sha256>` | none | Expected package bytes across an Editor restart |
| `--import-layout <absolute-file>` | none | Import a legacy layout if no current Editor layout exists |
| `--project <folder>` | none | Opens the project in this folder, in edit mode; its code, if it has any, is built first |
| `--game <path>` | none | Explicit compatible development game module; with neither project nor module, the start screen |
| `--seed <n>` | `1` | Seed of a game's World; a project's comes from its scene |
| `--select <name>` | none | Selects the entity with this name at the start |
| `--set <Type.field=value>` | none | After `--edit-after` seconds, sets a field of the selected entity through its inspector, in the displayed unit (a point's axis is the field's name with `X` or `Y`; a list of points is `x,y;x,y;...`); repeatable |
| `--edit-after <seconds>` | `1` | When the `--set` edits happen |
| `--quit-after <seconds>` | `0` | Closes the editor after this long; `0` means never |
| `--screenshot <file.png>` | none | With `--quit-after`, saves the last frame of the window |
| `--time-scale <x>` | `1` | Starts with simulated time at this speed; with `--quit-after`, the simulated time is logged at the end |
| `--mode <game\|select>` | `game`; `select` for a project | What the mouse does in the viewport at the start |
| `--paused <on\|off>` | `off` | Starts with time paused, so a World stays as built |
| `--after <process id>` | none | Waits, up to 15 seconds, for that editor to exit before opening; the editor passes it when it restarts itself |
| `--save-after <seconds>` | none | Saves the project after this long, after the `--set` edits |
| `--do <command>` | none | After the `--set` edits, one per frame, as the editor's gestures would: `create`, `duplicate`, `delete`, `undo`, `redo`, `select=<name>`, `select+=<name>` (adds to the selection), `rename=<name>`, `move=<dx>,<dy>` (world units), `add=<Type>`, `remove=<Type>`, `save`, `play`, `stop`, `drop=<file>[;<file>...]@<x>,<y>` (as files dropped at that point of the view, in pixels), `image=<file>` (as chosen with the selected sprites' **...**), `font=<file>` (as chosen with the selected texts' **...**), `addpoint=<Type.field>` and `removepoint=<Type.field>@<index>` (as **Add point** and **-** on a list of points); the entities, the selection with each sprite's image and each path's points, the history, and whether the scene is saved are logged after each; repeatable |
| `--recent <on\|off>` | `on` | Whether the project joins the recent projects; `off` for scripted runs, so they leave the user's list alone |

The times of `--edit-after`, `--save-after`, and `--quit-after` count from when the game or the
project opens, after any build of its code; the build screen is never closed by them.

A `--set` on a button presses it: `--select scenario --set BoidsScenario.count=300 --set
BoidsScenario.rebuild=go` rebuilds the boids with 300. For example, `--game C:/modules/boids-module.dll --select controls --set BoidControls.mode=Repel --set
BoidControls.radius=300` turns the boids away from a pointer with a 300-unit reach, as if edited by
hand in the inspector. With several entities selected, a `--set` changes all of them. Selection
gestures and the viewport menu's actions are logged.

Private development builds additionally expose `--dev-channel`,
`--dev-capture-dir`, `--dev-hidden`, `--quit-after-steps`, `--layout` and
`--reopen-probe`. These are separate from normal application use; see
the [development channel](development-channel.md) and
[profile selection](development.md).
