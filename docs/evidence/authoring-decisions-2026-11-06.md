# Recorded authoring decisions and compatibility chronology

**Document ID:** PIXIE-EDITOR-AUTHORING-EVIDENCE-20261106

**Schema version:** 1

**Kind:** audit

**Lifecycle:** historical

**Subject:** preserved authoring decisions and earlier compatibility observations

**Surface:** audit

**Boundary:** global

**Authority role:** evidence

**Mutation:** snapshot

**Indexing:** include

**Reference date:** 2026-11-06

**Planning baseline:** `c7cc2bfad332d70674cceb3e4474539ffccf528e` in PixieEditor; `1f44ae74008f1c2df4ff9b86ee8bb44f8ce8e072` in PixieEngine.

**Original sources:** Editor `README.md` and `docs/editor-movement.md`; Engine `TASKLIST.md`.

This record preserves observations from the named baselines. No new product
run, visual review or acceptance occurred during documentation reorganization.
It is not a closure of an entire proposal or a new task list. The original
text remains available with `git show <commit>:<path>` in its owning repository.

## Viewport, Session and Entities

The Engine task record accepted the bounded Viewport, Session and Entities
visual prototype on 2026-11-02 under PIXIE-TASK-231. The normal Editor candidate
had received technical qualification, independent review and owner UX review.
The wider task remained in progress; C++ authoring, Inspector fields and
World/Scene acceptance were separate. This corrects the old Editor README's
statement that the delivered layout still awaited that review, without closing
any remaining scope.

## World properties inspection

The Editor README recorded owner acceptance on 2026-11-03 of the bounded
Session-to-Inspector World-properties organization. Multiple Inspector panels
were explicitly outside the TODO-057 slice. The current workflow is in
[World properties](../world-and-scenes.md#world-properties).

## Development channel observations

The Editor README recorded local qualification with the official C# SDK client
and an Editor session driven by a Claude Code client. That observation does not
establish new evidence for a different package, SDK tuple or current client
version. The originating Engine record is
`docs/shared-engine-export-validation.md`; its environment and scope remain
part of the claim. The [channel guide](../development-channel.md) retains the
existing invocation recipe and operation boundaries.

## Movement compatibility chronology

The original movement guide recorded the following earlier delivery:

> C API **version 12** adds named layer queries, layer editor metadata and value-type
> flags. Version 11 added reflected record lists and typed item access. Version 10
> added admission queries and singleton metadata; version 9 added movement diagnostics
> and Edit-mode gizmo picking/bounds.
> Rebuild game C API libraries before loading them into the updated editor. The
> scene format remains version 1: the new components are additive data, and old
> libraries retain unknown components through the existing compatibility rules.

These are historical introduction versions. Current ABI/SDK admission and
explicit v1/v2 conversion follow the selected SDK and the
[World/Scene guide](../world-and-scenes.md#scene-authoring). Moving this paragraph
neither upgrades a project nor changes its format.
