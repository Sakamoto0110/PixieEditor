# Proposal: a friendly folder and file browser for the start screen

**Proposal ID:** EDITOR-PROPOSAL-001
**Status:** promoted to [EDITOR-RM-001](../../ROADMAP.md#editor-rm-001--friendly-folder-and-file-browser) on 2026-11-17
**Requested by:** the owner, in chat on 2026-11-17 (direct owner instruction; no shared workspace parent)
**Nature:** a deliberately small, disposable-first change that may later become a permanent feature

This document records the request and the reasoning behind the accepted outcome.
After promotion the [roadmap item](../../ROADMAP.md#editor-rm-001--friendly-folder-and-file-browser)
owns scope and acceptance, and TODO owned the work until closure; the completed group is
preserved in the [closure record](../history/editor-rm-001-folder-browser-2026-11-17.md).
This proposal is not otherwise updated as work progresses.

## Problem

The start screen asks for a folder three times: New project (location), Open
project, and Choose SDK. All three use `src/Ui/FolderBrowser.cs`, a single list of
folder names with an editable path, an Up button, Windows drive buttons and
New folder. It works, but it is not friendly:

- one undifferentiated list with no columns, sorting, type marks or dates;
- no Back or Forward, no clickable path segments, and only drive letters as places
  (no home, Desktop, Documents or Downloads);
- no filter in folders with hundreds of entries;
- a small, fixed list area and no keyboard navigation in the list;
- long names and recent-project paths are cut off without a hint.

## Proposed outcome

A reusable browser built on interfaces, so other Editor screens can adopt it:

- **File system access** behind an interface. The default implementation uses only
  portable .NET (`System.IO`, `Environment`); platform-specific calls are a last
  resort. A fake implementation drives tests without a window.
- **A model without UI**: current folder, entries, history, path segments, filter,
  natural sort, selection, and safe folder creation that never reuses an existing
  name.
- **A thin ImGui view** on the Editor's existing ImGui/OpenGL host: toolbar, editable
  and clickable path, places, a sortable table, filter, keyboard and clear errors.
- **Configuration by purpose**: folder or file mode, extensions, confirm label,
  optional entry marks (the existing "project" mark) and a confirm check.

The start screen's three pickers adopt it. The asset chooser keeps its current
behaviour through the same component in file mode.

## Out of scope

SDK validity, SDK compatibility messages and other SDK selection problems; native
OS dialogs; changes to project creation semantics; D09 (GLFW distribution), which
remains frozen in the Engine.

## Owner constraints recorded with the request

- The path field stays editable.
- Portable code first; Windows-specific code only as a last resort.
- Both current cases are folder pickers; file mode must be easy. A temporary
  button that opens Downloads in file mode may be used to try it and must be
  removed before closure.
- Creating a folder must refuse a name that already exists.
