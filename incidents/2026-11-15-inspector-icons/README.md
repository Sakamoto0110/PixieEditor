# Inspector GPU icons retained across native reload

Date: 15 November 2026. Finding: TR-01. Status: fixed locally and delivered in the public Editor; no remote publication.

## Failure and impact

The Inspector cached lock/chain texture IDs once with `IconsLoaded`. Successful code reload replaced the native session and disposed its predecessor, destroying that session's image textures. Rebuilding the Inspector kept the cached IDs. The UI subsequently displayed image fragments or checkerboard pixels when GL texture names were reused. PNG files existed in the output; embedding their bytes would not fix GPU lifetime.

This is a host resource-lifetime violation. It can make edit controls misleading after an otherwise successful reload. It does not establish the cause of historical native process exits, and it is independent of spritesheet building and animation playback.

## Correction

`src/Ui/InspectorPanel.Interactions.cs` keys the icon cache to `EngineSession.BindingRevision`. Binding a replacement clears the old pair before acquiring both images from the current session. A rejected candidate leaves the revision and valid current-session icons unchanged. Missing images cannot leave an expired pair assigned to new views. No native SDK, packaging, builder, animation or general asset-callback code changed.

## Focused verification

The public test executable has the following entry point:

```text
PixieEditor.AssetTests --inspector-icons-reload <installed-sdk-root> <catalog-sha256> <project-folder> <built-module-dll>
```

It uses a hidden OpenGL 4.5 context and the existing Inspector rebind method. It reads the texture references assigned to actual views, verifies liveness/dimensions, hashes GPU RGBA pixels before requesting images from the new session, and verifies current-session cache ownership. A prebuilt module avoids another compilation. The project target is unsaved test state.

The test covers initial distinct lock/chain pixels, same-session rebind, rejected missing-module admission, and two successful candidate render/publish/dispose cycles. [The old binary log](before-fix.log) fails after the first successful publication because the Inspector texture is dead (exit `-532462766`). [The fixed binary log](fixed.log) passes both reloads and the rejection control (exit `0`). The old test-process exception is the expected negative control, not a new Editor incident.

The new test build passed with zero warnings/errors; the public locked publish passed. Tested and delivered DLLs are byte-identical. This is native GPU/Inspector-reference verification, without a new visible UI round. General pending callbacks and post-publication recovery remain open findings.

## Exact delivery identity

- Source base: `ca1d8c20d60326adea7f90640bf2c02811a8944e`, plus captured pre-existing working changes and this fix.
- SDK: `0.3.1.0-baseline-7115d9f7`, public authoring/Release; catalog SHA-256 `1790c70aa1dcef0cff208eea49af452ae76d4eb979cb8bed50fa3d5ca92f1479`.
- Inspector package: `0.3.0-pixie021.3`, approved offline feed; development tools disabled.
- Prior DLL SHA-256: `1acc112d916c3001b15550ae76d2fa17b6ec84407c0eb97e4dcc9eb155a86985`.
- Fixed DLL SHA-256: `fc8af3831936e0c6b250c9d97854fbfa4a228d1dcebca2c29adb24ed12b1807c`.
- Module SHA-256: `12efd14c798dc2c0f6b65cc0aa39d5d7b1aa19fe29f0e82c9141ad3fd0fa2fa0`.
- Local evidence/build directory: `C:/Users/Nekovih/dev/PixieAssetTests/PXW-inspector-icons-20261115`; `RESULTS.json` retains complete input/output hashes, exact module path and preserved working changes. `build-command.json` and `test-arguments.json` retain invocations.
- Normal output: `src/bin/Release/net10.0`; all 50 delivered files matched the isolated stage. The prior output is retained under the evidence directory's `previous-main-bin`.

The complete delivered application includes other contributors' preserved dirty-source changes. This scoped commit does not claim a clean-commit reproduction of that entire output or promotion into a new approved baseline. No cleanup, SDK export or push accompanied delivery.
