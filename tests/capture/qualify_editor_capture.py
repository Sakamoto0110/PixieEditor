"""Run finite captures from an isolated collapsed-viewport layout; preserve the user's layout."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--editor", required=True)
    parser.add_argument("--work", required=True)
    parser.add_argument("--sdk", required=True)
    parser.add_argument("--sdk-manifest-sha256", required=True)
    parser.add_argument("--native-configuration", choices=("Debug", "Release"), required=True)
    fixture = parser.add_mutually_exclusive_group(required=True)
    fixture.add_argument("--project", type=Path)
    fixture.add_argument("--module", type=Path)
    parser.add_argument("--layout-template", type=Path)
    args = parser.parse_args()
    work = Path(args.work).resolve()
    work.mkdir(parents=True, exist_ok=True)
    run = Path(tempfile.mkdtemp(prefix="editor-capture-", dir=work))
    original = args.layout_template.resolve() if args.layout_template else None
    before = original.read_bytes() if original else None
    selection = ["--project", str(args.project.resolve())] if args.project else ["--game", str(args.module.resolve())]
    layout = before.decode("utf-8") if before else ""
    layout = re.sub(r"(?ms)^\[Window\]\[Viewport\]\r?\n.*?(?=^\[|\Z)", "", layout)
    layout += "\n[Window][Viewport]\nPos=50,50\nSize=500,400\nCollapsed=1\n\n"
    records = []
    for steps in (0, 1, 12):
        output = run / str(steps)
        output.mkdir()
        (output / "imgui.ini").write_text(layout, encoding="utf-8")
        result = subprocess.run([str(Path(args.editor).resolve()), "--sdk", str(Path(args.sdk).resolve()),
                                 "--sdk-profile", "local-development", "--native-configuration", args.native_configuration,
                                 "--sdk-manifest-sha256", args.sdk_manifest_sha256, *selection, "--seed", "42",
                                 "--layout", str(output / "imgui.ini"), "--dev-hidden", "on",
                                 "--quit-after-steps", str(steps), "--screenshot", str(output / "actual.png"),
                                 "--recent", "off"], cwd=output, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, encoding="utf-8", errors="replace", timeout=20)
        (output / "capture.log").write_text(result.stdout, encoding="utf-8")
        assert result.returncode == 0 and f"in {steps} steps" in result.stdout, result.stdout
        saved = (output / "imgui.ini").read_text(encoding="utf-8")
        viewport = re.search(r"(?ms)^\[Window\]\[Viewport\]\r?\n.*?(?=^\[|\Z)", saved)
        assert viewport and "Collapsed=1" in viewport.group(), saved
        assert (output / "actual.png").is_file()
        records.append({"case": f"collapsed-viewport-{steps}", "exit": 0, "steps": steps, "collapsed": True})
    assert original is None or original.read_bytes() == before
    records.append({"case": "user-layout-preserved", "sha256": hashlib.sha256(before).hexdigest() if before else None})
    (run / "results.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
    print(f"PASS: collapsed viewport captures 0/1/12; user layout unchanged. Evidence: {run}")


if __name__ == "__main__":
    main()
