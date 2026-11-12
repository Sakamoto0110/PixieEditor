# Editor documentation policy

The workspace's shared planning contract is adopted here so this repository
remains independently readable. The Editor owns its code, public contracts,
build, dependencies, versioning, release workflow and technical documentation.
Installed SDK requirements are explicit product contracts, not sibling paths.

The [Pixie workspace policy](../../../../.governance/workplaces/void-workplace/docs/governance/documentation-policy.md)
is the adopted parent for shared constraints and coordination. Its applicable
rules take precedence within that scope; Editor source, workflows, technical
contracts and measured evidence retain their product owners. Local branch and
release procedures remain specializations. Consuming an Engine SDK or Inspector
contract does not make that peer the parent of unrelated Editor decisions.
Unavailable inherited context is identified as unresolved, not silently waived.

## Authority and navigation

The [documentation index](../README.md) routes the guides. Each fact has one
owner; other documents summarize it and link to that owner.

| Fact | Owner |
|---|---|
| Application entry point and overview | Root `README.md` |
| Filesystem ownership and focused reading routes | Root `FILESYSTEM.md` |
| Implemented behavior | `src/` and the selected SDK's contract |
| Build properties, dependencies and artifact production | Project/build manifests and workflow definitions; `DEPENDENCIES.md` records dependency choices |
| Product version | Root `version.json` |
| Build, development and release procedures | `docs/development.md` |
| Editor workflows and session contracts | Topic guides under `docs/` |
| Editor direction, local outcomes and acceptance criteria | Root `ROADMAP.md` |
| Accepted Editor-local execution, dependencies and gates | Root `TODO.md`; optional `TASKLIST.md` decomposes large accepted items |
| Public native API and SDK compatibility | The Engine SDK and Engine-owned references; no duplicate Editor API catalog |
| Original source transfer | `docs/extraction-origin.md`, preserved at its recorded baseline |
| Recorded results, decisions and limitations | Records routed by `docs/evidence/README.md` and their original evidence owners |
| Shared governance and project profiles | Workspace configuration; machine-local bindings only route it |

Existing `PIXIE-TODO-*` references retain their Engine-origin identities and
original evidence owners. Current coordination ownership is established by the
canonical parent and explicit transfer record, not inferred from a namespace.
The local ROADMAP and TODO make the Editor contribution boundary explicit;
their presence does not approve new Editor-native scope. A shared contribution
identifies its parent and local responsibility. Pinned reference excerpts retain
source coordinates and baseline digests, are nonauthoritative, and do not create
a second scheduler. TASKLIST remains optional.

## Scheduling: `TODO.md` and `TASKLIST.md`

The workspace adopts ROADMAP -> TODO -> optional TASKLIST. ROADMAP owns the
big picture, rationale, phases and acceptance criteria. TODO records accepted
work needed to achieve those outcomes. TASKLIST decomposes only large items;
children reference their parent and do not expand its scope. Owner instructions
can authorize work; record the accepted scope without creating another queue.

Completion preserves the roadmap block and its outcome in one document under
`docs/history/`, linking evidence and incorporating only important nested details.
Record original work IDs and the last planning commit. Remove completed entries
from active TODO/TASKLIST; do not archive copies of all three lists. A completed
child does not close its active roadmap parent. Keep permanent rules and freezes
in their governing document. Legacy direct-owner work without a roadmap block
states that limitation instead of inventing one.

Use the history index to discover closures. Existing archive records remain
unchanged at their historical baselines. Evidence records distinguish requirements,
actual execution and limitations; a structural check cannot accept a product gate.

## Evidence and boundaries

Record actual source/version, environment, scenario, result and limitations.
Reference evidence from closure records without copying logs or claiming that a
build proves interactive behavior. Existing extraction and qualification
records retain their exact original scope. The current GLFW distribution
contract is recorded in [Dependencies](../../DEPENDENCIES.md).

Local planners distinguish approved product work from structural migration.
Do not invent work to fill them or require an empty TASKLIST. Current development
instructions remain in [the development guide](../development.md).

The [filesystem map](../../FILESYSTEM.md) and local guides remain readable in a
standalone checkout. Workspace catalogs and generated policy copies route to
authored owners; machine-local bindings and prepared copies have no independent
authored authority. Structural verification does not imply activation or a new
product, package, runtime or publication result.
