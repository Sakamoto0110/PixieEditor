# C++ component authoring

Select scene entities in Edit mode, open **Add component**, enter a new C++
component name, and choose **Create component and rebuild**. A project without
code receives the CMake scaffolding from the selected installed SDK. Component
implementations live in `src/components/`; the generated registry and source
list are tracked in `.pixie-code/manifest.json`.

The Editor waits for the previous process to release the module, builds against
the selected SDK and reopens the project. It restores unsaved authored data
and selection by scene ID without saving the scene. It verifies registration
before attaching a new type. Build failures offer retry and open-without-code.
Keep the local reopen handoff and `.pixie-code/backups/` when diagnosing an
interrupted operation.

An Inspector section with declared project source offers **Open code**. The
Editor validates the source path and opens the project and file in VS Code.
Install Microsoft's C/C++ and CMake Tools extensions, then select **Pixie Editor
(local)** as the configure preset. The preset uses the project's admitted
installed SDK and CMake build plan. Keep the machine-local
`CMakeUserPresets.json` out of version control. Conflicting user settings are
preserved and reported when opening the file.
