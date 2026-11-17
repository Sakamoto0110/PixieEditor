# EDITOR-RM-001 closure: friendly folder and file browser

**Closed:** 2026-11-17
**Work IDs:** EDITOR-PROPOSAL-001 → EDITOR-RM-001; EDITOR-TODO-001, EDITOR-TODO-002, EDITOR-TODO-003, EDITOR-TODO-004
**Child report:** EDITOR-RM-001-REPORT-1 (all four TODO items completed)
**Last planning commit:** `b35b6584cee4b4bdbac8dfcf40e2fc9f27a8e3e7` (proposal, roadmap item and TODO group)
**Implementation commit:** `959f5624c82e2cf1a4870735d90fc62a5e54cb6f`
**Branch:** `claude/start-screen-folder-browser` from `a2f4635` (not pushed)
**Authority:** direct owner instruction in chat on 2026-11-17, with a pre-flight that authorized proposal, promotion, implementation, a simulated pause, commit and closure

## Original roadmap block (as promoted)

**Rationale.** The start screen asks for a folder three times (New project location,
Open project, Choose SDK) through a bare list. A friendlier, reusable browser
removes that friction now and can serve other Editor screens later.

**Scope.** A browser component built on interfaces (file-system access, a model
without UI, a thin ImGui view, configuration by purpose), adopted by the three
start-screen pickers; the asset chooser keeps its behaviour through the same
component. Portable .NET first. Out of scope: SDK validity and other SDK
selection problems, native OS dialogs, project creation semantics, D09.

The [proposal](../proposals/start-screen-folder-browser.md) keeps the original request
and owner constraints.

## Acceptance criteria and dispositions

| # | Criterion | Disposition | Evidence |
|---|---|---|---|
| 1 | New project, Open project and Choose SDK use the new browser; the path stays editable | Met | `StartScreen.cs` uses `FolderBrowser` (now the facade); captures of Open project, New project and Choose SDK; the path is edited after **Edit path**, Ctrl+L or a click beside the segments |
| 2 | Back, Forward, Up, Refresh, clickable segments and places | Met for the model; drawn and visible | Model tests (history, Up, segments); captures show toolbar, segments and places (Home, Desktop, Documents, Downloads, C:, D:, E:) |
| 3 | Columns, natural sort by column, filter, project mark | Met | Model tests (sort, reverse, natural order, filter, mark); captures show Name/Modified (Size in file mode), project marks and a filtered list |
| 4 | Keyboard: Enter opens, Backspace goes up, arrows move the selection | Implemented; model behaviour tested; **not exercised interactively** | `FileBrowserView.HandleKeys` calls tested model operations; no interactive key session was run (owner check pending) |
| 5 | New folder refuses existing (case-insensitive) and invalid names, never overwrites | Met | Model tests with a fake file system and with the real one (existing folder, existing file kept byte-identical, empty/parent/nested names, non-ASCII name); the file-system layer itself also refuses an existing path |
| 6 | File mode through the same component; temporary Downloads button removed | Met | File-mode model tests; capture of Downloads in file mode taken with the temporary button's page; the button and its page were removed before the implementation commit (no `TEMPORARY` code remains) |
| 7 | Window-free model tests; existing start-screen test and Editor build pass | Met | `tests/folder-browser`: 33 checks passed; `tests/start-screen`: 11 checks passed with the 0.3.1.0 authoring Release engine; `dotnet build src/PixieEditor.csproj -c Debug`: 0 warnings, 0 errors (warnings are errors) |
| 8 | No new Windows-only dependency; platform branches isolated | Met | Only `System.IO`/`Environment`/`DriveInfo`; the single platform branch names roots (drive letters vs `/` and mount folders) in `LocalFileSystemView`; Windows trailing-dot rule kept as before |

## What was done

- `src/Ui/Browsing/`: `IFileSystemView` (with `BrowserEntry`, `BrowserPlace`),
  `LocalFileSystemView` (portable), `BrowserOptions` (mode, extensions, hidden,
  create-folder, mark), `FileBrowserModel` (navigation, history, segments, filter,
  natural sort, selection, confirm target, safe folder creation) with `NaturalComparer`,
  and `FileBrowserView` (ImGui toolbar, path, places, filter, sortable table, keyboard).
- `tests/folder-browser/`: window-free tests with an in-memory file system and a
  temporary real folder.

## What was updated

- `src/Ui/FolderBrowser.cs`: same entry point for its callers, now a facade over the model
  and view; adds `Target` (selected folder, else current) and `Model`.
- `src/Ui/StartScreen.cs`: New project and Open project use the selected folder as target;
  larger list areas; Choose SDK uses the target.
- `src/Ui/AssetChooser.cs`: adds a **Choose** button for a selected file (double-click and
  Enter also choose).
- `tests/start-screen/PixieEditor.StartScreenTests.csproj`: links the new browser files.
- `docs/projects.md`: user guide for the picker; `docs/README.md`: routes proposals and history.
- `ROADMAP.md` and `TODO.md`: promotion, then closure (this record).

## What was removed

- The previous single-list implementation inside `FolderBrowser.cs` (replaced, not kept).
- The temporary **Try file mode (Downloads)** button and its page in `StartScreen.cs`.
- A temporary, never-committed capture hook (`PIXIE_CAPTURE_PAGE`) used only to take
  screenshots of each page with `--screenshot`.
- The completed EDITOR-TODO-001..004 entries from the active `TODO.md`.

## Process and limitations

- The work ran in a separate worktree so the shared Editor checkout and its active branch
  were not switched. Nothing was pushed or published.
- A planned pause was taken at the owner's request (recorded as a quota pause; it was a
  simulation). VAW workflow record `editor-rm-001-folder-browser` and continuity checkpoint
  `editor-rm-001-pause-1` were written with the installed runtime scripts; on resume the
  checkpoint was compatible (no scoped file changed) and the workflow moved back to active.
  Both records are machine-local under the worktree's `.local/vaw/` and are not durable authority.
- Visual evidence is static: Editor `--screenshot` captures of each page, not an
  interactive session. Keyboard handling, double-click and New folder in the live UI remain
  for the owner to try (criterion 4).
- The Editor's `checkdocs.ps1` passed (0 problems); cross-repository links to the workspace
  governance were not validated from the worktree location.
- The build used the machine-local `.local/PixieEditor.props` SDK pins, not a VAW baseline
  `invoke`; no SDK selection or baseline state was changed.
