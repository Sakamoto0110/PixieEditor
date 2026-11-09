# Local development channel

[Documentation index](README.md)

Development builds accept `--dev-channel <name>` and
`--dev-capture-dir <folder>`. The former opts into one local Windows client;
the latter fixes its capture destination. The native pipe handle rejects remote
clients and uses a protected owner-SID ACL. Without the opt-in flag, no listener
starts. These channel options/types are excluded from a shipping editor publish.

`tools/pixie-mcp/` in PixieEngine is the separate .NET 10 stdio server/client bridge. Obtain the bridge from the Engine tooling and pass the independently built
Editor executable, installed private SDK and project explicitly:

```powershell
dotnet C:/tools/PixieMcp.dll --kind editor --host C:/PixieEditor/PixieEditor.exe --sdk C:/sdk/private --native-configuration Debug --project C:/projects/sprites --captures C:/captures/editor
```

The server answers its client at once and opens the host in the background; the
first tool call waits for the host's channel. A project with code is built before
the editor opens its channel. The wait lasts up to `--startup-timeout <seconds>` (600 by default). A host that exits
first, or a deadline that passes, makes that call a tool error with
`possibly_applied: false`. The host runs in a Windows job of the server: it closes
with the server, also when the server is killed.

To register it with a client, use absolute paths, since the client chooses the
working directory. Claude Code, for the current user:

```powershell
claude mcp add pixie-editor -- dotnet C:/tools/PixieMcp.dll --kind editor --host C:/PixieEditor/PixieEditor.exe --sdk C:/sdk/private --native-configuration Debug --project C:/path/to/project --captures C:/path/to/captures
```

Codex, in `~/.codex/config.toml`:

```toml
[mcp_servers.pixie-editor]
command = "dotnet"
args = ["C:/tools/PixieMcp.dll", "--kind", "editor",
        "--host", "C:/PixieEditor/PixieEditor.exe", "--sdk", "C:/sdk/private", "--native-configuration", "Debug",
        "--project", "C:/path/to/project", "--captures", "C:/path/to/captures"]
tool_timeout_sec = 900
```

A client's per-call time limit has to cover the first call's wait for a project
with code: `tool_timeout_sec` in Codex, `MCP_TOOL_TIMEOUT` (milliseconds) in
Claude Code. Each registration opens one project; `--kind player` with a game's
player, or `pixie-player`, serves only `world.*`.

The [recorded local qualification](evidence/authoring-decisions-2026-11-06.md#development-channel-observations)
used the official C# SDK client and a Claude Code client. That client shows
tools as `mcp__<server>__world_hello` (dots become underscores). The player offers `world.*`;
the editor also offers `inspector.*` and `editor.*`. World writes use native
operations without Inspector history. Inspector writes use the same Set and
editing-command handlers as scripted runs, with actual recorder/history effects.
Use scene IDs and a current generation from `inspector.tree` for channel writes,
not names or runtime-index labels. Entity-reference inputs must also satisfy
their native References metadata.

Inspector observations distinguish held/view values from optional live/storage
reads. Custom text/point/record/mask rows identify their native-storage domain;
they are not advertised as generic held Inspector values. Receipts distinguish
pending, refusal, no-op, stale target and possible partial effects, preserving
command-local native failures before later reads. Handler true alone is not
write acceptance. Read-side failure diagnostics are separate from cached or
previous write failures.

Capture returns full-window PNG image content after a coherent subsequent UI
frame. Its alpha-one render does not pause or discard fractional time. Names
cannot escape the fixed folder and existing files are not overwritten. Export,
build, import/drop and font/image filesystem gestures are unavailable through the
channel, even though ordinary CLI/UI editing retains its own behavior. No
screenshot is an approved snapshot reference merely because a capture succeeded.

`editor.do` with `save` writes the scene through the same handler as Ctrl + S: in
Play it writes the scene as it was at Play, and its receipt reports `unsaved`.
`world.hello` reports it in `capabilities.save`. An editor built with
`-p:PixieChannelSave=false` leaves the gesture out, and the channel refuses it;
the editor's own Save is unchanged. World commands never save, in the player or
the editor.

## Qualification recipe

The driver belongs to PixieEngine at `tests/mcp/qualify_mcp.py`, not this
Editor repository. The commands below assume that tooling repository as the
working directory and use explicitly supplied Editor/SDK artifacts. They
document the existing recipe; moving this guide does not schedule a replay.

Use the supplied
schema 1 artifact and fixture inventory for one private `local-development`
SDK configuration, and an absolute work directory outside the inputs. Use
separate inventories and work roots for Debug and Release. The Windows replay
requires `dotnet`, PowerShell, and a desktop session with OpenGL 4.5.

```powershell
py tests/mcp/qualify_mcp.py --inputs "C:\qualification\inputs.json" --work "C:\qualification\work" --preflight-only
py tests/mcp/qualify_mcp.py --inputs "C:\qualification\inputs.json" --work "C:\qualification\work"
```

The inventory identifies the exact SDK catalog and profile. Its `artifacts`
object has six required keys: `editorSaveOn`, `editorSaveOff`, `bridge`,
`client`, `writeProbe` and `channelTests`. Each entry supplies an absolute
`root`, a root-relative `entry` and a complete `files` inventory with paths,
sizes and SHA-256 hashes. The `fixtures` object supplies `noCode` and
`legacyScene`. See the qualification driver (PixieEngine tests/mcp/qualify_mcp.py)
for the full schema fields.
Preflight checks the identities and file hashes without launching hosts. The
full run stages copies in a fresh run directory under `--work` and records
player, Editor, pipe, physics and owned-host cleanup results in `results.json`.

Set `cold.status` to `deferred` with a reason until the exact private authoring
handoff and never-built success and deadline fixtures are supplied. A deferred
cold suite is recorded as omitted. See the
historical qualification record (PixieEngine docs/shared-engine-export-validation.md)
for earlier source-build runs and their scope.
