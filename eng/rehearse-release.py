"""Verify Engine release inputs and build the local Windows x64 Editor candidate."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo

ROOT = Path(__file__).resolve().parents[1]
PAIR = "0.3.0-pixie021.3"
INSPECTOR = ("PixieInspector", "PixieInspector.ImGui")
LOCK = ROOT / "src/packages.inspector-selected.lock.json"
NATIVE = ROOT / "eng/native/win-x64"
SHA256 = re.compile(r"[0-9a-f]{64}")
VERSION = re.compile(r"[0-9]+(?:\.[0-9]+){3}")
DEVICE = re.compile(r"(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|(?:COM|LPT)[1-9¹²³])", re.IGNORECASE)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path, algorithm="sha256"):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, algorithm).digest()


def sha(path):
    return digest(path).hex()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8", newline="\n")


def unpack_sdk(assets, target, archive_hash, catalog_hash):
    receipt = json.loads((assets / "release.json").read_bytes())
    version = receipt["productVersion"]
    require(receipt["schemaVersion"] == 1 and VERSION.fullmatch(version), "Invalid Engine release receipt")
    require(receipt["sdkCatalogSha256"] == catalog_hash, "Engine receipt catalog hash mismatch")
    archive = assets / f"PixieEngine-SDK-{version}-windows-x64.zip"
    entries = [entry for entry in receipt["archives"] if entry["name"] == archive.name]
    require(len(entries) == 1 and entries[0]["sha256"] == archive_hash, "Engine receipt archive hash mismatch")
    require(archive.stat().st_size == entries[0]["size"] and sha(archive) == archive_hash, "SDK ZIP hash mismatch")

    target.mkdir()
    with ZipFile(archive) as zip_file:
        catalog_bytes = zip_file.read("sdk.json")
        require(hashlib.sha256(catalog_bytes).hexdigest() == catalog_hash, "SDK catalog hash mismatch")
        catalog = json.loads(catalog_bytes)
        require(catalog["schemaVersion"] == 1 and catalog["distributionClass"] == "public"
                and catalog["sdkId"] == receipt["sdkId"] and catalog["productVersion"] == version,
                "SDK catalog does not match the public Engine release receipt")
        declared = {"sdk.json": (len(catalog_bytes), catalog_hash)}
        for item in catalog["files"]:
            require(item["path"] not in declared, "Duplicate SDK inventory path")
            declared[item["path"]] = (item["size"], item["sha256"])
        seen = set()
        for member in zip_file.infolist():
            name = member.filename
            path = PurePosixPath(name)
            folded = name.casefold()
            require(name == member.orig_filename and name in declared and folded not in seen and not member.is_dir()
                    and not stat.S_ISLNK(member.external_attr >> 16)
                    and not path.is_absolute() and path.as_posix() == name
                    and all(part not in (".", "..") and not part.endswith((".", " "))
                            and not DEVICE.fullmatch(part.split(".", 1)[0].rstrip(" "))
                            and not re.search(r'[\\:<>"|?*\x00-\x1f]', part) for part in path.parts),
                    f"Unsafe, duplicate or undeclared SDK ZIP member: {name}")
            seen.add(folded)
            size, expected = declared[name]
            require(member.file_size == size, f"SDK member size mismatch: {name}")
            dest = target.joinpath(*path.parts)
            dest.parent.mkdir(parents=True, exist_ok=True)
            with zip_file.open(member) as source, dest.open("xb") as output:
                shutil.copyfileobj(source, output)
            require(sha(dest) == expected, f"SDK member hash mismatch: {name}")
        require(len(seen) == len(declared), "SDK ZIP is missing catalog members")
    subprocess.run([sys.executable, "-B", target / "tools/sdk/verify_sdk.py", target, "--complete"], check=True)
    shipping = json.loads((target / "profiles/shipping/Release/profile.json").read_bytes())
    require(shipping["sdkBuildId"] == receipt["shippingBuildId"], "Shipping build ID mismatch")
    return receipt, shipping


def prepare_feed(feed, upstream, work, dependencies):
    selected = work / "inspector"
    selected.mkdir()
    for name in INSPECTOR:
        entry = dependencies[name]
        package = feed / f"{name}.{PAIR}.nupkg"
        require(entry["resolved"] == PAIR and
                base64.b64encode(digest(package, "sha512")).decode("ascii") == entry["contentHash"],
                f"Inspector package hash mismatch: {name}")
        shutil.copyfile(package, selected / package.name)

    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="inspector", value=str(selected))
    ET.SubElement(sources, "add", key="upstream", value=upstream)
    mapping = ET.SubElement(config, "packageSourceMapping")
    ET.SubElement(mapping, "clear")
    for key in ("inspector", "upstream"):
        source = ET.SubElement(mapping, "packageSource", key=key)
        for name in dependencies:
            if (name in INSPECTOR) == (key == "inspector"):
                ET.SubElement(source, "package", pattern=name)
    config_path = work / "NuGet.Config"
    ET.ElementTree(config).write(config_path, encoding="utf-8", xml_declaration=True)
    return config_path


def stage_editor(publish, stage, sdk, authoring, shipping, packages, dependencies):
    for file in sorted(publish.rglob("*")):
        relative = file.relative_to(publish)
        if not file.is_file() or file.suffix == ".pdb":
            continue
        if relative.parts[0] == "runtimes" and relative.parts[1] != "win-x64":
            continue
        name = file.name.casefold()
        require(name not in {"pixie.dll", "pixie-player.exe", "opengl32.dll"} and
                not name.startswith(("pixie-mcp", "pixiemcp", "modelcontextprotocol", "pixie-export")),
                f"Unexpected Editor publish payload: {relative}")
        destination = stage / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(file, destination)

    for entry in shipping["nativeDependencies"]:
        source = sdk / entry["path"]
        if source.name.startswith(("msvcp", "vcruntime", "concrt")):
            shutil.copyfile(source, stage / source.name)
    licenses = stage / "licenses"
    shutil.copyfile(ROOT / "LICENSE", licenses / "PixieEditor.txt")
    shutil.copyfile(sdk / "notices/Microsoft-runtime/compiler-runtime.txt", licenses / "Microsoft-runtime.txt")
    for name, entry in dependencies.items():
        package = name.lower()
        version = entry["resolved"]
        with ZipFile(packages / package / version / f"{package}.{version}.nupkg") as archive:
            if "LICENSE.txt" in archive.namelist():
                (licenses / f"{name}.txt").write_bytes(archive.read("LICENSE.txt"))

    provenance = json.loads((NATIVE / "release-provenance.json").read_bytes())
    native = stage / "runtimes/win-x64/native"
    glfw_path = "profiles/authoring/Release/bin/glfw3.dll"
    glfw = [entry for entry in authoring["nativeDependencies"] if entry["path"] == glfw_path]
    require(len(glfw) == 1 and sha(sdk / glfw_path) == glfw[0]["sha256"], "SDK GLFW provider mismatch")
    provenance["binaries"]["glfw3.dll"] = {
        "sha256": glfw[0]["sha256"], "provider": "PixieEngine SDK",
        "sdkPath": glfw_path, "sdkBuildId": authoring["sdkBuildId"],
        "sdkCatalogSha256": sha(sdk / "sdk.json"), "notice": "licenses/glfw.txt",
    }
    require({file.name for file in native.iterdir()} == provenance["binaries"].keys(), "Unexpected Windows x64 native inventory")
    for name, entry in provenance["binaries"].items():
        require(sha(native / name) == entry["sha256"], f"Native Editor payload changed: {name}")
    for relative, expected in provenance["notices"].items():
        source = NATIVE / relative
        require(sha(source) == expected, f"Native notice changed: {relative}")
        shutil.copyfile(source, licenses / source.name)
    require(sha(licenses / "glfw.txt") == sha(sdk / "notices/glfw/LICENSE.md"), "SDK GLFW notice mismatch")
    write_json(stage / "native-provenance.json", provenance)
    require((stage / "PixieEditor.exe").is_file(), "Editor apphost missing")


def rebuild_editor_native(work, stage, sdk):
    recipe = NATIVE / "rebuild"
    output = work / "native-build"
    native = stage / "runtimes/win-x64/native"
    subprocess.run(["pwsh", "-NoProfile", "-File", recipe / "build.ps1", "-SdkRoot", sdk,
                    "-BaselineNative", native, "-Out", output], check=True)
    build_file = output / "native-build.json"
    build = json.loads(build_file.read_bytes())
    provenance_file = stage / "native-provenance.json"
    provenance = json.loads(provenance_file.read_bytes())
    expected = provenance["binaries"].keys() - {"glfw3.dll"}
    require(build["schemaVersion"] == 1 and build["pinsSha256"] == sha(recipe / "pins.json") and
            build["sdkCatalogSha256"] == sha(sdk / "sdk.json") and
            build["sdkGlfwSha256"] == provenance["binaries"]["glfw3.dll"]["sha256"] and
            build["brotliDecoder"]["staticLinkConfirmed"] is True and
            build["binaries"].keys() == expected, "Rebuilt native receipt does not match the candidate inputs")
    for name, entry in build["binaries"].items():
        source = output / "bin" / name
        require(sha(source) == entry["sha256"] and sha(native / name) == entry["baselineSha256"],
                f"Rebuilt native file does not match its receipt: {name}")
        shutil.copyfile(source, native / name)
        provenance["binaries"][name] = dict(entry, provider="Pinned PixieEditor native build")
    shutil.copyfile(build_file, stage / build_file.name)
    provenance["qualification"] = "pinned native rebuild with verified ABI and notices"
    provenance["nativeBuild"] = {"path": build_file.name, "sha256": sha(build_file)}
    provenance["unresolved"] = []
    write_json(provenance_file, provenance)


def pack(stage, assets, receipt, sdk, dependencies):
    version = json.loads((ROOT / "version.json").read_bytes())["version"]
    require(VERSION.fullmatch(version), "Invalid Editor version")
    authoring = json.loads((sdk / "profiles/authoring/Release/profile.json").read_bytes())
    manifest = {
        "schemaVersion": 1, "productId": "pixieeditor", "productVersion": version,
        "platform": "windows", "architecture": "x64",
        "editorSourceCommit": os.environ.get("GITHUB_SHA"),
        "systemPrerequisites": ["Windows x64", ".NET Runtime 10 x64", "OpenGL 4.5 core driver", "Vulkan loader (Hexa backend dependency)"],
        "engineSdkArchiveSha256": next(entry["sha256"] for entry in receipt["archives"]
                                       if entry["name"] == f"PixieEngine-SDK-{receipt['productVersion']}-windows-x64.zip"),
        "engineSdkCatalogSha256": receipt["sdkCatalogSha256"], "engineSdkId": receipt["sdkId"],
        "engineAuthoringBuildId": authoring["sdkBuildId"], "engineShippingBuildId": receipt["shippingBuildId"],
        "managedDependencyLockSha256": sha(LOCK),
        "dependencies": {name: {"version": entry["resolved"], "nugetContentHash": entry["contentHash"]}
                         for name, entry in dependencies.items()},
        "files": [{"path": file.relative_to(stage).as_posix(), "size": file.stat().st_size, "sha256": sha(file)}
                  for file in sorted(stage.rglob("*")) if file.is_file()],
    }
    write_json(stage / "editor.json", manifest)
    assets.mkdir()
    archive = assets / f"PixieEditor-{version}-windows-x64.zip"
    with ZipFile(archive, "x") as zip_file:
        for entry in [*manifest["files"], {"path": "editor.json"}]:
            path = entry["path"]
            zip_file.writestr(ZipInfo(path), (stage / path).read_bytes(), compress_type=ZIP_DEFLATED)
    with ZipFile(archive) as zip_file:
        for entry in manifest["files"]:
            require(hashlib.sha256(zip_file.read(entry["path"])).hexdigest() == entry["sha256"],
                    f"Editor ZIP member mismatch: {entry['path']}")
        require(zip_file.read("editor.json") == (stage / "editor.json").read_bytes(), "Editor manifest mismatch")
    release = {"name": archive.name, "size": archive.stat().st_size, "sha256": sha(archive),
               "editorManifestSha256": sha(stage / "editor.json"),
               "engineSdkCatalogSha256": receipt["sdkCatalogSha256"]}
    write_json(assets / "release.json", release)
    (assets / "SHA256SUMS.txt").write_text(f"{release['sha256']}  {archive.name}\n", encoding="ascii")
    print(archive)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--engine-assets", required=True, type=Path)
    parser.add_argument("--sdk-archive-sha256", required=True, type=str.lower)
    parser.add_argument("--sdk-catalog-sha256", required=True, type=str.lower)
    parser.add_argument("--inspector-feed", required=True, type=Path)
    parser.add_argument("--upstream-feed", type=Path, help="Optional offline mirror of the locked NuGet.org dependencies")
    parser.add_argument("--rebuild-native", action="store_true", help="Build the pinned Windows x64 ImGui DLLs")
    parser.add_argument("--out", required=True, type=Path, help="New staging directory")
    args = parser.parse_args()
    require(os.name == "nt", "This rehearsal requires Windows x64")
    require(SHA256.fullmatch(args.sdk_archive_sha256) and SHA256.fullmatch(args.sdk_catalog_sha256),
            "Supply trusted SHA-256 values for the SDK ZIP and catalog")
    work = args.out.resolve()
    work.mkdir()
    sdk = work / "sdk"
    receipt, shipping = unpack_sdk(args.engine_assets.resolve(), sdk, args.sdk_archive_sha256, args.sdk_catalog_sha256)
    dependencies = json.loads(LOCK.read_bytes())["dependencies"]["net10.0"]
    config = prepare_feed(args.inspector_feed.resolve(),
                          str(args.upstream_feed.resolve()) if args.upstream_feed else "https://api.nuget.org/v3/index.json",
                          work, dependencies)
    publish = work / "publish"
    packages = work / "nuget"
    env = os.environ | {"NUGET_PACKAGES": str(packages)}
    subprocess.run(["pwsh", "-NoProfile", "-File", ROOT / "eng/build.ps1",
                    "-SdkRoot", sdk, "-CatalogSha256", args.sdk_catalog_sha256,
                    "-InspectorPackageVersion", PAIR, "-NuGetConfig", config,
                    "-Locked", "-PublishDirectory", publish], cwd=ROOT, env=env, check=True)
    stage = work / "stage"
    authoring = json.loads((sdk / "profiles/authoring/Release/profile.json").read_bytes())
    stage_editor(publish, stage, sdk, authoring, shipping, packages, dependencies)
    if args.rebuild_native:
        rebuild_editor_native(work, stage, sdk)
    pack(stage, work / "assets", receipt, sdk, dependencies)


if __name__ == "__main__":
    try:
        main()
    except (KeyError, OSError, ValueError, subprocess.CalledProcessError) as error:
        sys.exit(f"Editor rehearsal refused: {error}")
