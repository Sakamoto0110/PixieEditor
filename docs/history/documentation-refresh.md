# PixieEditor documentation refresh

**Lifecycle:** historical
**Roadmap source:** Direct owner instruction on 2026-11-12. No separate roadmap block was assigned.
**Planning baseline:** `d1760e14549da826fecddec3550244123f9ea715`.
**Work IDs:** Direct documentation work.
**Outcome:** Completed the English Editor source documentation, architecture and authoring guide refresh.
**Evidence:** Integrated receipt `.local/d261112/C0/evidence/REVIEW.md` in the Engine workspace and the diagnostic below.
**Limitations:** Local Windows x64 build and documentation evidence. Human usability and release qualification were outside the recorded checks.

## Preserved roadmap

The owner requested current Editor workflow and source documentation for
session publication, selection, history, reflection, code creation and reopen,
recovery and export. The scope covers guides and comments. Existing product
and shared acceptance criteria retain their original owners.

## Acceptance disposition

| Criterion | Result |
| --- | --- |
| Source and content preservation | All 29 source changes are comments. The token and directive comparison covers 16 preprocessor combinations. The old-content ledger records destinations, corrections and retained historical material. |
| Reader routes | The [documentation index](../README.md) and [architecture guide](../architecture.md) provide routes to current authoring and implementation sections. Two independent exercises verified the question routes. The standalone exercise used separately obtained guide files and installed SDK declarations. |
| Builds and links | Public Release and private Debug builds passed locked restore and compilation with the corresponding fresh SDKs. Editor and workspace documentation checks passed with untracked pages included. |
| Compound conversion | The current v3 limitation is described in the [movement guide](../editor-movement.md#compound-bodies) and the diagnostic below. The scope remains documentation. |

The Editor's versioning profile applies `void.versioning/2` to its own
version file. This documentation scope is `REVISION` class. D09 remains frozen.

## Compound conversion diagnostic

In the retained Editor diagnostic, converting one selected static `Collider2D`
parent to a compound in an admitted v3 scene detached its unselected child.
The child kept its stable scene ID and world pose. Undo restored the parent's
original collider and left the child detached, with its former world position
stored as a root position. Redo kept that detached state. The leaf control
restored its authored scene on undo.

The diagnostic ran at Editor `d1760e14549da826fecddec3550244123f9ea715`
with public `authoring/Release` SDK catalog SHA-256
`20cc87ee7ca0f3c4d9f5fa6f9fbc4ee6e14064a819c01f3ce090ea41d02f12bc`.
The retained `REVIEW.md` in the Engine checkout's
`.local/validation/u3-compound-repro-20261111-231754` has SHA-256
`1e7985011bcc22e0c055fe20af6ceeabeba54fb87f1d56cde63249ff745eba5f`.
The diagnostic exercised one hierarchy and one leaf control. Save after
conversion and other hierarchy shapes were outside its observed cases.
