"""Check generated cimgui signatures and layouts against the locked Hexa inputs."""

import json
import sys
from pathlib import Path

FREE_TYPE = {
    "ImGuiFreeType_DebugEditFontLoaderFlags": ("bool", ("ImGuiFreeTypeLoaderFlags*",)),
    "ImGuiFreeType_GetFontLoader": ("const ImFontLoader*", ()),
    "ImGuiFreeType_SetAllocatorFunctions": (
        "void", ("void*(*)(size_t sz,void* user_data)", "void(*)(void* ptr,void* user_data)", "void*")),
}


def normalize(value):
    if isinstance(value, dict):
        return {key: normalize(item) for key, item in value.items() if key != "comment"}
    if isinstance(value, list):
        return [normalize(item) for item in value]
    if isinstance(value, str):
        return value.replace("__cdecl", "")
    return value


def read(path):
    return json.loads(path.read_bytes())


def signatures(folder):
    return {item["ov_cimguiname"]: (item.get("ret"), tuple(normalize(arg["type"]) for arg in item["argsT"]))
            for group in read(folder / "definitions.json").values() for item in group}


reference, generated = map(Path, sys.argv[1:])
expected = signatures(reference)
actual = signatures(generated)
if actual.keys() & FREE_TYPE.keys():
    sys.exit("The generated FreeType exports overlap the explicit compatibility exports.")
actual.update(FREE_TYPE)
if expected != actual:
    sys.exit(f"Hexa signature mismatch: {sorted(key for key in expected.keys() | actual.keys() if expected.get(key) != actual.get(key))}")

layouts = read(reference / "structs_and_enums.json")
rebuilt = read(generated / "structs_and_enums.json")
layouts["enums"].pop("ImGuiFreeTypeLoaderFlags_")
for section in ("structs", "enums"):
    if normalize(layouts[section]) != normalize(rebuilt[section]):
        sys.exit(f"Hexa {section} mismatch.")

print(f"Matched {len(expected)} signatures, {len(layouts['structs'])} structures and {len(layouts['enums'])} enums.")
