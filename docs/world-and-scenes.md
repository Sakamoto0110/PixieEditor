# World, Scenes and presentation

[Documentation index](README.md)

## World properties

**Session > World properties** selects a distinct World target in the existing Inspector.
For projects retaining the legacy representation, Physics and Environment sections bind their unique v1 settings owners;
the Environment section contains ambient light and fog, while exposure, bloom and renderer
quality remain in their existing component editor. No proxy entity or missing capability is
created by navigation. Absent settings are shown explicitly; conflicting legacy settings are
diagnosed without choosing an editable owner. Runtime-generated settings and Play-mode fields
are read-only in this view.

Inspecting World does not change the viewport's Environment choice. Selecting an entity returns
the same Inspector to entity editing. World inspection survives Undo/Redo, Play/Stop and the
private code-reopen handoff; bindings reject access after their World generation is replaced.
The [recorded owner decision](evidence/authoring-decisions-2026-11-06.md#world-properties-inspection)
accepted this bounded organization on 2026-11-03. Multiple Inspector panels
remain outside that accepted slice.

The development Inspector channel currently supports entity field targets. When the GUI inspects
World or Scene, `inspector.tree` reports that target with an explicit unsupported reason and no entity
rows; its entity property writes and entity-bound gestures refuse before their handler. The validated
`select=<scene ID>` gesture remains available to return to entity inspection with a fresh
generation. Generic Inspector field writes remain entity-only. The separate
`editor.do worldpresentation=...`, `scenepresentation=...` and
`presentationmigrate=confirm` gestures author presentation through the shared,
generation-checked candidate coordinator; `editor.presentation` reads its catalog.

## Scene authoring

**Entities > Scenes** lists logical Scene scopes and a World-properties target. Selecting a
Scene opens its name and stable ID in the same Inspector; it does not filter visibility,
redirect input or select the viewport Environment. **+ Scene** creates an empty scope using
the entered name. Scene names commit as one queued edit when text activity finishes, including
when the user switches target or collapses Inspector. The queue carries stable IDs and its
original session/content/history token; an intervening edit refuses the expired command.

**Remove empty Scene** refuses an occupied scope. Select its entities and change **Scene
membership** to another Scene or **Directly in World** first. Multiselection transfers form
one candidate/history entry and reject entirely if any member is generated, missing or
duplicated. Entity IDs, component reference payloads and unknown data remain unchanged.
Creating an entity still places it directly in World; Scene selection does not implicitly
assign new entities. Scene Environment and gravity properties are authored separately below. Bodies retain global gravity
until physical-technique approval; temporary unloading is not an implemented lifecycle.

Scene authoring requires an open fallback or explicitly contracted module project in Edit.
C++ projects opt in with `pixie_game(... CANDIDATE_AUTHORING SOURCES ...)`; existing projects
are not enabled automatically. Custom authored components need the restricted native
`AddData<T>` contract; ordinary registrations and authored Behaviour components stay refused for v2
until they have a supported contract. Unsupported type names are reported without partial
import. Rebuild/reopen replaces the process/module and restores authored data, membership,
Scene inspection and Environment intent with fresh bindings and an empty Undo/Redo history.
It does not copy mutable Project instance state or preserve history across code recompilation.
After a failed module load, retrying that same repaired path is supported; selecting a different
library requires restarting and displays a diagnostic instead of silently selecting fallback.
On a v1 project, use
**Convert to v2... > Convert and save** first. The confirmation explicitly saves current
unsaved authorship, validates the converted result and preserves the exact original file
beside it as `.v1-backup`. A pre-existing backup/staging conflict refuses conversion; no
existing backup is overwritten. Conversion does not replace World storage, clears dirty
after saving and expires prepared content tokens. It does not create a format-undo step;
existing entity history remains usable. Ordinary v1 projects remain v1 until that explicit
action. Unsupported code projects keep existing component editing and do not gain candidate
authoring authority.

Normal Undo/Redo routes Scene steps, v2 structural fragments and v2 Inspector gestures through
complete-document candidate history. New v1 interaction groups retain full snapshots for safe
replay after explicit conversion while preserving v1 replay behavior. Raw v2 fragment restoration
is refused before destructive work; legacy entity-only startup handoffs cannot replace a v2 World. Distinct Scene inspection survives candidate
recovery, Play/Stop and the private code-reopen handoff. Removal falls back to World inspection;
Undo restores the same Scene ID and target. Viewport Environment intent remains independent.

The command line's `--do` and development channel's `editor.do` share these serial handlers:
`scenecreate=<name>`, `sceneselect=<16-digit scope ID|world>`,
`scenerename=<16-digit scope ID>:<name>`, `sceneremove=<16-digit scope ID>`,
`scenemove=<16-digit scope ID|world>` for the selected entities, and
`sceneconvert=confirm` for explicit conversion/save. The channel requires a fresh generation
from `inspector.tree`. `editor.scenes` lists scope IDs/names, format version and current scope;
`editor.selection` distinguishes entity IDs from `scope_id`. Conversion is unavailable in
channel builds with `PixieChannelSave=false`. Inspector field writes remain entity-only;
Scene commands and global Undo/Redo can operate from World/Scene targets.

## Entity parenting

The admitted v3 scene family persists parent links and child-local poses in
unscoped scenes. v1 remains flat and v2 supports logical Scene scopes. A change
to the v3 family uses a new destination. Existing v1 and v2 files can remain
in their formats. [Engine v3 scene contract](https://github.com/Sakamoto0110/PixieEngine/blob/ac48e01ed040ee607662e6cdf1f4ff9742c4b0f4/docs/modules/runtime/REFERENCE.md#version-3-parenting-scenes)

A version 3 project opened with a parenting-capable SDK lists only roots in
**Entities**. The Inspector shows the hierarchy immediately above **Add
component**, using the same header style as the components. Click a name to
show its components inside that branch. Open branches keep their components
visible when you select another entity in the tree. Use the arrows to expand
or collapse branches. Ctrl-click adjusts the selection and shows shared
components once above the tree. The Entities list highlights the selected
entity's root.

In Edit mode, right-click a tree row and use **Set parent** to choose a parent
or **None (make root)**. The same menu offers **Duplicate branch**.
The operation keeps the current world position, rotation and scale. The
Transform2D fields show world values, while the saved scene stores local
values for attached entities. **Duplicate branch** copies the entity and its
descendants together. Undo/Redo restores links and poses in one step. **Build
and reopen** restores unsaved links and selection across the process restart.
History starts fresh after that restart.

## Component field groups and lists

A matching SDK can declare collapsible field groups through
`ComponentBuilder::Group`. Slash-separated labels such as `Motor/State`
open nested sections with the same header style as components. Group labels
affect presentation. The project keeps the existing saved field names.

Generic record lists use **Item** rows and **Add item**. A read-only list
shows its entries with editing disabled. The compound collider's shape list
keeps its shape-specific controls.

Projects can label computed fields **Derived** and live inspection fields
**Runtime**. Both groups are read-only. Runtime fields appear in Play. The
scene keeps authored settings, and the project regenerates the other values.
Project-specific field locks also apply when moving entities in the viewport.

When the selected entity has a project-supplied circle, **Show rotation
envelope** displays it in Edit. An active edit can show the previous circle
and the change in diameter.

If an authored project raises a game error during a simulation step, the
Editor attempts to return to the authored Edit scene through the same
path as **Stop**. The error appears in the status bar. If restoring Edit
also fails, playback stays halted and **Stop** can retry the restore.

## Canonical presentation and authored cameras

World properties can explicitly migrate a v2 project's legacy lighting adapter to
canonical presentation settings. The candidate transaction consumes the legacy
LightingSettings2D component and retains its full opaque preimage as compatibility
data. It does not create placeholder entities. Undo/Redo restores one representation
at a time. A document containing both canonical settings and the legacy component
is rejected before mutation. v1 opening/saving is unchanged; format conversion
remains explicit with its recoverable original backup.

Environment owns ambient light and fog. Primary view owns exposure, tone mapping
and bloom; Renderer owns quality and resource budgets. AO and normal-map values
retain their prior rendering behavior in a read-only compatibility block pending
the owner's remaining ownership decisions. Scene properties can inherit or override
Environment values, retaining inactive authored values. A Scene override never
installs or enables a missing/disabled World lighting capability. The viewport's
Environment choice remains independent of selection and camera choice.

Camera2D is an authored component: Transform2D.position is its center, with a
finite zoom in the native supported range and an enabled flag. World and Scene
default camera references use stable entity SceneIds. The viewport chooses free
editor camera, defaults for an explicitly named Scene/World, or an explicit camera.
Resolution tries the explicit camera, the explicitly associated Scene default,
World default, then the existing free/session camera. Missing/disabled references
produce a diagnostic without discarding intent; undo can restore the same identity.
Rendering, coordinate conversion, picking and gizmos use that same resolution.
Play/Stop and code reopen preserve choice and free-camera center/zoom.

The editor still has one viewport window. Native serial callers can render/pick
multiple independent camera/Environment contexts against one World, using separate
render targets and optional per-view post-processing values. These calls restore
the primary host context afterward. This is not a parallel renderer, streaming
policy, spatial blend or additional Inspector implementation.

## Session publication and recovery

Serial full-document Scene editing uses one coordinator. It reserves snapshot history before native publication, invalidates
old bindings on success, and retains committed history when rebinding needs an explicit retry.
Its edits refuse unfinished gestures and pending recorder changes through a session gate shared
with GUI/scripted authoring, history, Inspector writes and development dispatch. Its history
entries can be replayed only through that coordinator. During committed binding recovery,
ordinary mutations, project asset imports and stale Inspector queries refuse before their
handlers; read-only `world.*` observations remain available. `editor.do` with
`{"command":"recover"}` retries bindings without replaying the World or history; the reply
distinguishes `recovered` from `recovery_pending`. The panel offers **Retry Inspector recovery**.
The unsaved-close prompt still permits discard/cancel and disables Save until recovery; the
existing scripted timed exit remains available.

Preparation and publication are separate serial calls. The immutable prepared value owns its
before/after document strings and session/content/storage/history identity, never live World,
Inspector or descriptor references. Publication rechecks all identities and captures current
selection. Ordinary field edits expire prepared data even without a storage replacement.
This boundary launches no workers and does not make native reconstruction thread-safe.
The coordinator accepts the Engine fallback and explicitly contracted C++ modules admitted by
C API ABI28 (module contract1 was introduced in ABI27). Module contract1 reconstructs matching Project/descriptors/World instances and
requires candidate-local callbacks and nonthrowing local teardown; arbitrary native external
effects are outside its guarantee. See the runtime contract (PixieEngine docs/modules/runtime/REFERENCE.md#edit-and-play).
Ordinary component-field editing keeps
its existing path. Qualification measures serial edit latency and process peak memory across
World sizes. Complete snapshots use an owner-thread reusable managed byte buffer, initially
64 KiB and bounded to 128 MiB plus the terminator: a fitting capture serializes once; overflow
retries once with fresh native data. Capacity is retained until session disposal and measured
separately from process peaks. Native data is never cached between captures. v2 history retains
the original snapshot string instead of parsing and serializing it again; v1 in-memory history
promotion and the explicit backed-up on-disk conversion gate remain separate.
Multithreaded loading remains deferred until its backend and lifetime contracts are qualified.
