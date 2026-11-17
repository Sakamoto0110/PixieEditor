# PixieEditor roadmap

**Document ID:** GLOBAL-ROADMAP

**Schema version:** 1

**Kind:** roadmap/status

**Lifecycle:** current

**Subject:** Editor product boundary and local contribution outcomes

**Surface:** roadmap

**Boundary:** global

**Authority role:** roadmap

**Mutation:** authored

**Indexing:** include

## Product direction and ownership

PixieEditor is the independent authoring application for projects, scenes and
C++ components consumed through an explicitly selected installed Engine SDK.
It owns Editor UI, session composition, loading, project workflows and history.
The Engine retains native runtime behavior, the public native API and SDK
compatibility. Inspector retains its generic inspection contracts; the Editor
owns their application-level integration.

The [filesystem map](FILESYSTEM.md) routes these boundaries. Shared coordination
rules come from the [workspace policy](../../.governance/workplaces/void-workplace/docs/governance/documentation-policy.md);
Editor build and release procedures remain in [development](docs/development.md).

## Local outcome admission

No separately approved new Editor-native product outcome was introduced by the
documentation continuation; later owner-approved outcomes are listed under
[Accepted local outcomes](#accepted-local-outcomes). Creating local planners establishes ownership and
discovery; it does not authorize product implementation or qualify existing work.

An accepted local outcome must state its rationale, scope and acceptance criteria.
An Editor contribution to a shared outcome must identify the canonical workspace
parent, the Editor-owned portion and its dependencies. Preserve existing origin
IDs and original evidence ownership. A pinned project reference excerpt may aid
reading, but it is nonauthoritative and cannot replace the parent's current
scope, criteria or status.

[TODO](TODO.md) owns accepted local execution and gates. TASKLIST is optional
for large local items; no task breakdown is required merely by this file's presence.

## Accepted local outcomes

### EDITOR-RM-001 — Friendly folder and file browser

**Work ID:** EDITOR-RM-001
**Parent:** none
**Authority:** direct owner instruction on 2026-11-17; no shared workspace parent
**Status:** active
**Origin:** [EDITOR-PROPOSAL-001](docs/proposals/start-screen-folder-browser.md)
**Child report:** none

**Rationale.** The start screen asks for a folder three times (New project location,
Open project, Choose SDK) through a bare list. A friendlier, reusable browser
removes that friction now and can serve other Editor screens later.

**Scope.** A browser component built on interfaces (file-system access, a model
without UI, a thin ImGui view, configuration by purpose), adopted by the three
start-screen pickers; the asset chooser keeps its behaviour through the same
component. Portable .NET first. Out of scope: SDK validity and other SDK
selection problems, native OS dialogs, project creation semantics, D09.

**Acceptance criteria.**

1. New project, Open project and Choose SDK use the new browser; the path stays editable.
2. Back, Forward, Up, Refresh, clickable path segments and places (home, Desktop,
   Documents, Downloads, drives or mounts) work.
3. Entries show type, modification time and size where meaningful, sort by column
   in natural order, can be filtered by typing, and keep the project mark.
4. Keyboard: Enter opens, Backspace goes up, arrows move the selection.
5. New folder refuses an existing name (case-insensitive) and invalid names, and never overwrites.
6. File mode works through the same component; the temporary Downloads button is removed.
7. Model behaviour is covered by tests that need no window; the existing start-screen
   test and the Editor build pass.
8. No new Windows-only dependency; any platform branch is isolated and justified.

Work: [TODO](TODO.md#start-screen-folder-browser-editor-rm-001).

## Preserved constraints

Existing [Editor workflows](docs/README.md#use-the-editor), build instructions,
SDK selection and source/provenance records remain unchanged by this structural
migration. The original Engine-derived work references retain their identities;
consult their canonical coordination owner rather than deriving current ownership
from the namespace or an old quotation.

D09 remains frozen under the [Editor policy](docs/governance/documentation-policy.md#evidence-and-boundaries).
[Recorded evidence](docs/evidence/README.md) describes named baselines, not new
acceptance. This roadmap grants no product tests, release, publication or activation.

