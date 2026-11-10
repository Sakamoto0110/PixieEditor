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

No separately approved new Editor-native product outcome is introduced by this
documentation continuation. Creating local planners establishes ownership and
discovery; it does not authorize product implementation or qualify existing work.

An accepted local outcome must state its rationale, scope and acceptance criteria.
An Editor contribution to a shared outcome must identify the canonical workspace
parent, the Editor-owned portion and its dependencies. Preserve existing origin
IDs and original evidence ownership. A pinned project reference excerpt may aid
reading, but it is nonauthoritative and cannot replace the parent's current
scope, criteria or status.

[TODO](TODO.md) owns accepted local execution and gates. TASKLIST is optional
for large local items; no task breakdown is required merely by this file's presence.

## Preserved constraints

Existing [Editor workflows](docs/README.md#use-the-editor), build instructions,
SDK selection and source/provenance records remain unchanged by this structural
migration. The original Engine-derived work references retain their identities;
consult their canonical coordination owner rather than deriving current ownership
from the namespace or an old quotation.

D09 remains frozen under the [Editor policy](docs/governance/documentation-policy.md#evidence-and-boundaries).
[Recorded evidence](docs/evidence/README.md) describes named baselines, not new
acceptance. This roadmap grants no product tests, release, publication or activation.

