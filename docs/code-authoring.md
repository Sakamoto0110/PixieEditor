# C++ component authoring

Select scene entities in Edit mode, open **Add component**, enter a new C++
component name, and choose **Create component and rebuild**. A project without
code receives the CMake scaffolding from the selected installed SDK. Component
creation writes `src/components/<Name>.hpp` and `src/components/<Name>.cpp`.
It updates `src/PixieComponents.generated.cpp` and `.pixie-code/Components.cmake`
and records generated file ownership in `.pixie-code/manifest.json`. Edit the
component source files. The generated registry and source list belong to the
generator.

## Build and reopen

The Editor waits for the previous process to release the module, builds against
the selected SDK and reopens the project. It restores unsaved authored data
and selection by scene ID without saving the scene. It verifies registration
and admission for every original target before attaching a new type. The new
session creates a new Project instance and starts fresh Undo/Redo history. A
successful attachment can create a new undo entry. The build screen offers
**Try again** and **Open without code**.
Keep the local reopen handoff and `.pixie-code/backups/` when diagnosing an
interrupted operation.

## Open code in VS Code

An Inspector section with declared project source offers **Open code**. The
Editor validates the source path and opens the project and file in VS Code.
On Windows it checks the default user and system install locations, then PATH
entries, including the VS Code `bin` wrapper's parent directory. If the IDE setup
reports a conflict or missing build information, read the notice in the Editor.
You can open the project's user-owned files in another code editor manually.
Install Microsoft's C/C++ and CMake Tools extensions, then select **Pixie Editor
(local)** as the configure preset. The preset uses the project's admitted
installed SDK and CMake build plan. Keep the machine-local
`CMakeUserPresets.json` out of version control. Conflicting user settings are
preserved and reported when opening the file.

Open code adds missing CMake Tools provider settings for the existing project.
CMake supplies the compiler, language standard, includes and defines; the
Editor does not guess Engine source locations.

## Entity input and native contracts

KeyboardControl/MouseControl are generic per-entity device components;
KeyboardControl2D remains an explicit legacy adapter. The Engine owns the
[public input, callback and registration contract](https://github.com/Sakamoto0110/PixieEngine/blob/ac48e01ed040ee607662e6cdf1f4ff9742c4b0f4/docs/entity-input-and-code-components.md).
The ABI 20 introduction is historical. Current builds must match the selected SDK. See
[development](development.md) for binding and package inputs and
[World/Scene authoring](world-and-scenes.md#scene-authoring) for candidate
reconstruction, explicit conversion and code-reopen limitations.
