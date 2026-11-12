# Editor filesystem and authority map

**Document ID:** GLOBAL-FILESYSTEM

**Schema version:** 1

**Kind:** reference

**Lifecycle:** current

**Subject:** Editor ownership, shared governance and focused reading routes

**Surface:** index

**Boundary:** global

**Authority role:** non-normative

**Mutation:** authored

**Indexing:** include

The Editor owns its UI, project workflows and session composition. The selected
Engine SDK owns native behavior, public native API and compatibility contracts.
The [workspace policy](../../.governance/workplaces/void-workplace/docs/governance/documentation-policy.md)
applies to shared coordination; the [Editor policy](docs/governance/documentation-policy.md)
owns local specialization. This map adds no build/runtime requirement for an
Engine source checkout or a particular sibling-folder layout.

## Where to work

| Area | Owner and direct reading route |
|---|---|
| `src/` | Editor UI, sessions, loading, history and project workflows. See [architecture](docs/architecture.md) and the [topic index](docs/README.md). |
| `src/PixieEditor.csproj` | Editor build properties and selected package inputs; [development](docs/development.md) |
| `eng/`, `.github/` | Editor build/release automation and declared artifact inputs; [development](docs/development.md), [dependencies](DEPENDENCIES.md) |
| `eng/native/` | Admitted Editor native dependency inputs and provenance; [evidence index](docs/evidence/README.md) |
| `tests/` | [Candidate publication](tests/code-components/SceneCandidateTests.cs), [process reopening](tests/code-components/ProcessReopenTests.cs) and [field interactions](tests/field-interactions/Program.cs). See [development](docs/development.md) for the supported checks and limits. |
| `docs/` | Editor user workflows, session contracts and evidence routes; [documentation index](docs/README.md) |
| Root planners | [ROADMAP](ROADMAP.md) owns local direction and contribution outcomes; [TODO](TODO.md) owns explicitly accepted local execution |
| Selected Engine SDK | Native public API, runtime and generated interop contract; consumed through declared SDK selection, not copied into an Editor API catalog |
| Inspector packages | Generic inspection and view contracts; Editor guides own their host integration |
| `docs/extraction-origin.md` | Historical source-transfer baseline, not a competing current workflow |
| Local build output and `.local/` when present | Generated artifacts, execution state or retained evidence; no independent authored authority and no deletion permission |

The [workspace entry](../../.governance/workplaces/void-workplace/README.md) and
[project catalog](../../.governance/workplaces/void-workplace/docs/catalog/README.md)
route cross-project work. Shared coordination and project implementation have
separate owners. Historical Engine-origin work IDs remain stable; their namespace
does not by itself establish the current coordination owner.

## Planning and evidence

The new local planners establish the Editor's contribution boundary. They do
not approve new Editor-native product work or reactivate completed work.
Project contribution records must identify their canonical shared parent and
local scope. A pinned reference excerpt is nonauthoritative and cannot become
a second scheduler; consult the canonical owner for current scope and status.
TASKLIST is optional; the [current breakdown](TASKLIST.md) contains the existing
Editor contributions transferred under their workspace parents.

Existing [evidence and history](docs/evidence/README.md) retain their original
owners, bytes and limitations. Shared structural checks cannot prove a new
Editor workflow, package, runtime or publication result. D09 remains frozen
under the [Editor policy](docs/governance/documentation-policy.md#evidence-and-boundaries).

## Standalone reading

The [root README](README.md), [development guide](docs/development.md) and local
technical guides remain usable without sibling checkouts. An unavailable
workspace parent or consumed peer route is unresolved context, not permission
to waive its applicable requirements. Generated workspace policy copies and
catalogs route to authored owners; they do not create independent authority.

