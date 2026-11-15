# Public sprite authoring delivery and reload intersection

15 November 2026. This is historical local acceptance, separate from implementation of build/reload and the Inspector icon fix.

The review source at `C:/Users/Nekovih/dev/PixieAssetTests/PXW-sprite-inspector-review-20261114` contained a working spritesheet builder and animation workflow. Comparison found those features already present in canonical source at `ca1d8c2`. No feature port was needed. Fourteen differing files represented other work; copying the entire review tree would have risked replacing unrelated current changes.

The normal public application was rebuilt from captured canonical inputs with installed SDK `0.3.1.0-baseline-7115d9f7`, authoring/Release, Inspector `0.3.0-pixie021.3`, development tools disabled and explicit catalog/input pins. The previous output was backed up; 50 delivered files matched the stage. All 179 canonical input files and 162 original review files remained unchanged. The output was delivered to `src/bin/Release/net10.0`. This did not promote a new VAW baseline.

| Existing focused suite | Passing checks |
| --- | ---: |
| Project assets | 21 |
| Animation model | 7 |
| Atlas animation playback | 30 |
| Spritesheet builder | 34 |

All four processes exited zero. Desktop acceptance in a disposable project verified the seven-frame sliced sheet, advancing animation preview, FPS edit/save/reopen, viewport playback, and repeat opening from the habitual public executable. No development/MCP Editor was used. Shipping export/dependency closure passed; standalone exported-player playback was not exercised.

A separate UI round performed three actual changed-code reloads in one process: with the builder open, with an atlas selector pending and with a PNG selector pending. The builder retained its animation draft (FPS 11), continued previewing and saved it. Old Sprite-specific atlas/PNG callbacks discarded their selection; fresh selectors accepted the same assets. Loaded-module inventories proved actual publication rather than an unchanged-code no-op. The PNG checkerboard in the empty direct-image fixture was expected.

Lock/chain corruption was separately observed during those reloads and corrected later: see [the icon incident](../../incidents/2026-11-15-inspector-icons/README.md). Passing Sprite guards does not qualify all pending callbacks or post-publication recovery. Packing/saving tasks completing across publication and every schema-change scenario were not exercised.

Original sprite-delivery DLL SHA-256: `1acc112d916c3001b15550ae76d2fa17b6ec84407c0eb97e4dcc9eb155a86985`, subsequently superseded locally by the icon-only correction. SDK catalog SHA-256: `1790c70aa1dcef0cff208eea49af452ae76d4eb979cb8bed50fa3d5ca92f1479`.

Retained exact manifests/commands/logs are under `C:/Users/Nekovih/dev/PixieAssetTests/PXW-sprite-preserve-20261115` (`INPUTS.json`, `TEST-RESULTS.json`, `DELIVERY.json`, `FINAL-VERIFICATION.json`) and `PXW-sprite-reload-20261115` (`INPUTS.json`, `modules-*.json`, `RESULTS.json`). These are local artifacts, not published-package acceptance. The source and application from the original review remain preserved. The delivered application includes pre-existing working changes, which this record does not claim to own or commit.
