# Evidence and history

Records describe the baseline that produced them. Build, package, interactive
use, visual acceptance and publication are distinct claims. A documentation
move does not rerun a scenario or close an outstanding acceptance item.

## Records owned here

| Record | Scope |
|---|---|
| [Extraction origin](../extraction-origin.md) | Preserved import commit, native binding ownership and original GLFW transfer |
| [Authoring decisions and compatibility chronology](authoring-decisions-2026-11-06.md) | Previously recorded UX/World decisions, client observations and ABI9–12/v1 introduction notes |
| [Native release provenance](../../eng/native/win-x64/release-provenance.json) | Admitted DLLs, notice inputs and outstanding provenance evidence |

## Earlier records retained in PixieEngine

The Engine repository retains the source history and coordinated qualification
records. These are repository coordinates, not a required sibling filesystem
layout or a promise that local commits have been published.

| Record | Source coordinate |
|---|---|
| SDK/Runtime/Editor separation and cutover | PixieEngine `TASKLIST.md` and `TODO.md` at `1f44ae74008f1c2df4ff9b86ee8bb44f8ce8e072` |
| Current route to the relocated separation record | PixieEngine `docs/evidence/sdk-editor-separation-2026-11-06.md` |
| World/Scene closure | PixieEngine `docs/archive/todo-057-world-scene-reconciliation-2026-11-04.md` |
| Earlier shared-engine/MCP qualification | PixieEngine `docs/shared-engine-export-validation.md` |

Use `git show <commit>:<path>` in the owning repository for its recorded
planning state. The Editor guide migration started at
`c7cc2bfad332d70674cceb3e4474539ffccf528e`. The original README and movement
notes remain traceable there; no full copy of the old README or schedulers
was added to an archive.

## Recipes are not results

[Field-interaction qualification](../editor-field-interactions.md#qualification),
[development-channel qualification](../development-channel.md#qualification-recipe)
and [release rehearsal](../development.md#rehearse-the-windows-x64-release)
describe inputs and procedures. They do not prove a new execution or grant
permission to run one. Existing evidence retains its recorded candidate scope.

The [history index](../history/README.md) routes completed Editor outcomes.

New roadmap closures will use `docs/history/` when an accepted outcome needs a
closure record, under the [documentation policy](../governance/documentation-policy.md).
D09's existing implementation/qualification freeze remains unchanged.
