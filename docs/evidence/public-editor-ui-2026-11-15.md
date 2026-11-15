# Public Editor UI acceptance after the Inspector icon fix

15 November 2026. Direct owner-requested verification; no new roadmap outcome or product implementation. The existing public executable passed this bounded interactive smoke test, including full process re-opening. No unexpected exit, failed build, corrupted Inspector icon or lost saved value was observed. This does not close other audit findings or qualify every Editor feature.

## Tested artifact and environment

- Executable: `C:/Users/Nekovih/dev/VAW/projects/Pixie/PixieEditor/src/bin/Release/net10.0/PixieEditor.exe`; no development build or MCP channel.
- Editor DLL SHA-256: `fc8af3831936e0c6b250c9d97854fbfa4a228d1dcebca2c29adb24ed12b1807c`.
- Selected Engine SDK: `0.3.1.0`, authoring/Release, revision `7115d9f7`; catalog SHA-256 `1790c70aa1dcef0cff208eea49af452ae76d4eb979cb8bed50fa3d5ca92f1479`.
- Actual SDK root, confirmed by CMake and loaded GLFW: `C:/Users/Nekovih/dev/VAW/.local/worktrees/pxpublic-031-triggers/Editor-public/sdk`. This is the retained workspace package, not the AppData SDK installation. Matching catalog hashes do not prove that every byte of both packages matches or that selection is centralized.
- OpenGL 4.5 on AMD Radeon RX 580 2048SP, non-debug context.
- Process 27292 performed both reloads; after normal closure, process 31568 reopened the project through Recent projects. Both launches used no command-line arguments.

## Interactive results

| Scenario | Observed result |
| --- | --- |
| Open workspace-local disposable project | Normal folder browser, initial code build, scene and sprite rendering succeeded. |
| Frames and animation | Builder showed seven 32-by-32 frames; preview advanced through visibly different poses. |
| Atlas save | Animation 2 changed from 11 to 12 FPS and reopened at 12. |
| Reload with builder open | Builder opened during compilation. Its 13 FPS draft and active preview remained usable after module replacement; saving afterward succeeded. |
| Inspector textures | Lock/chain icons remained recognizable after both actual changed-module reloads. |
| Play/Pause/Step/Stop | Animation/time advanced, pause held, Step changed time from 5.70 to 5.72 seconds, Stop restored Editing/time zero. |
| Pending atlas selector | A selector opened before the second reload discarded its stale selection afterward. A new selector accepted the same alternate atlas as a source-change draft; Cancel restored the original. |
| History and scene save | Sprite FPS changed from 14 to 15; Undo restored 14, Redo restored 15, and Save returned Scene Saved. |
| Fresh process | Reopening preserved sprite FPS 15, atlas Animation 2 FPS 13, rendering and Inspector icons. |

## Evidence and preservation

The full report, launch records, module inventories, build/runtime logs and final comparison remain at `C:/Users/Nekovih/dev/VAW/.local/editor-ui-20261115`. `REPORT.md` SHA-256 is `65a675ab436dff3ffe9e4f26aaff0ecbab47bf9ca8c68e3c9a93334f7161247f`; `FINAL-CHECK.json` SHA-256 is `966ec2f24ee4dbd97e6a0359cf45e952282f59a23117cccc626029fa8581659b`.

Loaded project DLL hashes were `175d67b82a332a72cb40041de7ce16f8dc901ad01f32a186942060c2e12f6594`, `525f02f7a15dd65862baa9cf0ca7181eca30fe37f213af4924b0a8af6bdd3c41`, and `129c14b0ba7d086dbcda9b36b72c13163f263059a117d5754815ee6ede1eb195`. Distinct loaded DLLs in the same process establish real replacement rather than unchanged-code reuse.

External sprite fixtures were read-only inputs. The disposable project, source probe, reports, temporary files and builds stayed in the workspace-local run. Normal `%APPDATA%/PixieEditor` configuration writes were explicitly approved. All 81 protected files compared unchanged, and the four repository HEADs/status lists matched their pre-test snapshots. Other contributors' pending code was not committed by this verification. The delivered executable retains its previously recorded composite input provenance; this evidence commit is not a clean-source reconstruction.

## Limits

MSBuild emitted MSB8029 for output under the process temporary directory; builds completed. Undo restored the value but left the scene dirty until Save. The Editor title's `0.3.0` is distinct from the Engine SDK's `0.3.1.0`. Exact desktop coordinates were not independently confirmed.

New-project creation, atlas creation from scratch, trigger authoring, compile-failure recovery, generic non-Sprite callbacks, post-publication failure recovery and long-duration stability were not exercised. This visible round supplements [the focused icon regression](../../incidents/2026-11-15-inspector-icons/README.md) and [earlier sprite acceptance](../archive/sprite-authoring-validation-2026-11-15.md); it does not replace their limits. No baseline promotion, SDK rebuild, implementation fix, cleanup or push was performed.
