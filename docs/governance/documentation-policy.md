# Editor documentation policy

The workspace's shared planning contract is adopted here so this repository
remains independently readable. The Editor owns its code, public contracts,
build, dependencies, versioning, release workflow and technical documentation.
Installed SDK requirements are explicit product contracts, not sibling paths.

## Authority and navigation

The [documentation index](../README.md) routes the guides. Each fact has one
owner; other documents summarize it and link to that owner.

| Fact | Owner |
|---|---|
| Application entry point and overview | Root `README.md` |
| Implemented behavior | `src/` and the selected SDK's contract |
| Build properties, dependencies and artifact production | Project/build manifests and workflow definitions; `DEPENDENCIES.md` records dependency choices |
| Product version | Root `version.json` |
| Build, development and release procedures | `docs/development.md` |
| Editor workflows and session contracts | Topic guides under `docs/` |
| Public native API and SDK compatibility | The Engine SDK and Engine-owned references; no duplicate Editor API catalog |
| Original source transfer | `docs/extraction-origin.md`, preserved at its recorded baseline |
| Recorded results, decisions and limitations | Records routed by `docs/evidence/README.md` and their original evidence owners |
| Shared governance and project profiles | Workspace configuration; machine-local bindings only route it |

Existing `PIXIE-TODO-*` references identify work coordinated in PixieEngine.
Keep those identities and their evidence; documentation migration does not
transfer a queue or create a second task owner. ROADMAP/TODO/TASKLIST are
created here only when accepted Editor-local work needs their respective roles.

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
records retain their exact original scope. D09 remains frozen; this governance
change does not authorize GLFW implementation, tests or publication.

Planning files are created when accepted work needs them, not as empty mandatory
scaffolding. Current development instructions remain in [the development guide](../development.md).
