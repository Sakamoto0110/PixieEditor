# Disk browser contract and visual handoff

Project selection, new project location, SDK selection, asset file selection, and asset folder import use the same navigation model. `src/Editing/FolderBrowser.cs` owns filesystem state and has no dependency on ImGui. The current `FolderBrowserView` in `src/Ui/FolderBrowser.cs` binds that model to the existing widgets.

| Contract | Behavior |
| --- | --- |
| `Current`, `Entries`, `CurrentIsProject` | Last readable directory and sorted typed entries. A project folder contains a `.pixie` descriptor. Files are visible only in file mode and only for the requested extension. |
| `Go(path)` | Accepts absolute paths and paths relative to `Current`. A directory becomes the current location; a matching file sets `ChosenFile` for the caller. |
| `Back()`, `Forward()`, `Up()`, `Refresh()` | Navigate or reread the snapshot. Failed navigation preserves the last directory, listing and history; `Error` describes the failure. New directory navigation clears forward history. |
| `Places` | Existing paths supplied by the host and caller: Home, Desktop, Documents, Windows drives, recent project locations, the SDK installation parent, or project/asset locations. A place activates via `Go(place.Path)`. |
| `CreateFolder(name)` | Creates a single direct child and enters it. Invalid names and existing files/folders report an error. |

The view owns the editable address text, focus, enabled/disabled presentation, shortcut bindings, keyboard navigation and scrolling. The current adapter supports Ctrl+L for the address, Alt+Left/Right for history, Alt+Up for the parent and F5 to refresh. It still renders the existing drive row; `Places` is ready for a richer visual treatment.

Claude can replace the presentation in `FolderBrowserView` and keep the same model. Suggested visual work: an address bar that can be edited or displayed as breadcrumbs, visible Back/Forward/Up/Refresh actions, and a places sidebar using the supplied paths. Show the error beside the control that triggered it and preserve the previous listing on failure. Keep file confirmation, project detection, SDK admission, and the caller's create/open/import/cancel behavior separate: the browser navigates the disk; each caller decides what the selection means.

Acceptance: use a path with spaces, a place, the address bar, history and a missing path in Start Screen > Open project, New project and Choose SDK, and in the asset file and folder import dialogs. Test matching and wrong extensions in the file chooser only. Verify Ctrl+L and navigation shortcuts stay within the focused browser; Escape retains the surrounding dialog's meaning. Keep the SDK picker's validation feedback visible when a folder can be browsed but cannot be used as an SDK.
