#!/usr/bin/env python3
"""
Offline syntax/semantic check of the project's hand-written URP shaders with Microsoft DXC.

Unity is not required: every HLSLPROGRAM block of every pass is extracted from the .shader files, combined with
its HLSLINCLUDE blocks and compiled (vertex + fragment) against the real URP / Core ShaderLibrary shipped with the
Unity editor, for several keyword variants. This catches typos, missing functions and type errors before opening
Unity. It does not validate ShaderLab syntax (Properties / render states); Unity does that on import.

Usage:
    DXC=/path/to/dxc UNITY_PACKAGES=/path/to/Editor/Data/Resources/PackageManager/BuiltInPackages \
        python3 Tools/ShaderCheck/check_shaders.py [shader files...]

Defaults: DXC=dxc on PATH, UNITY_PACKAGES=/opt/unity6/Editor/Data/Resources/PackageManager/BuiltInPackages,
shaders = Assets/_BreathOfEclipse/Shaders/*.shader
"""
import glob
import os
import re
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DXC = os.environ.get("DXC", "dxc")
PACKAGES = os.environ.get("UNITY_PACKAGES", "/opt/unity6/Editor/Data/Resources/PackageManager/BuiltInPackages")
LINKED = ["com.unity.render-pipelines.core", "com.unity.render-pipelines.universal", "com.unity.render-pipelines.universal-config"]

BASE_DEFINES = [
    "SHADER_API_D3D11=1", "UNITY_VERSION=60003", "SHADER_TARGET=45", "UNITY_UV_STARTS_AT_TOP=1",
    "UNITY_COLORSPACE_GAMMA_OFF=1",
    # Unity's preprocessor expands UNITY_BUILTINS_WITH_WORLDTOOBJECTARRAY before token pasting in
    # UNITY_ACCESS_INSTANCED_PROP; clang (DXC) pastes first. Provide the pasted name so instanced variants compile.
    "UNITY_BUILTINS_WITH_WORLDTOOBJECTARRAYArray=unity_Builtins0Array",
]


def make_package_root(tmp):
    root = os.path.join(tmp, "include_root")
    pkg = os.path.join(root, "Packages")
    os.makedirs(pkg, exist_ok=True)
    for name in LINKED:
        src = os.path.join(PACKAGES, name)
        if not os.path.isdir(src):
            sys.exit(f"Missing Unity package {src} (set UNITY_PACKAGES)")
        os.symlink(src, os.path.join(pkg, name))
    return root


def blocks(text, start, end):
    out = []
    pos = 0
    while True:
        i = text.find(start, pos)
        if i < 0:
            return out
        j = text.find(end, i)
        out.append((i, text[i + len(start):j]))
        pos = j + len(end)


def pragma_value(code, name):
    m = re.search(r"#pragma\s+" + name + r"\s+(\w+)", code)
    return m.group(1) if m else None


def keyword_lines(code):
    lines = []
    for m in re.finditer(r"#pragma\s+(?:multi_compile|shader_feature)(?:_local)?(?:_vertex|_fragment)?\s+([^\n]+)", code):
        opts = [o for o in m.group(1).split() if o != "_" and not o.startswith("__")]
        if opts:
            lines.append(opts)
    return lines


def variants(code):
    lines = keyword_lines(code)
    result = [[]]
    first = [opts[0] for opts in lines]
    last = [opts[-1] for opts in lines]
    if first:
        result.append(first)
    if last and last != first:
        result.append(last)
    if "_CLUSTER_LIGHT_LOOP" in sum(lines, []):
        result.append(["_MAIN_LIGHT_SHADOWS_CASCADE", "_CLUSTER_LIGHT_LOOP", "_SHADOWS_SOFT", "_SCREEN_SPACE_OCCLUSION", "FOG_EXP2"])
    if "multi_compile_instancing" in code:
        result.append(["INSTANCING_ON"])
    uses_fog = "Fog.hlsl" in code or "multi_compile_fog" in code
    if uses_fog:
        for v in result[1:]:
            v.append("FOG_EXP2")
    return result


def prepare(code):
    code = code.replace("#include_with_pragmas", "#include")
    # DXC does not know Unity's pragmas; drop them to keep the output readable.
    code = re.sub(r"^\s*#pragma[^\n]*$", "", code, flags=re.M)
    return code


def compile_stage(dxc, source_path, include_dirs, profile, entry, defines, stage_define):
    args = [dxc, "-nologo", "-HV", "2018", "-T", profile, "-E", entry, "-Wno-unused-value", "-Wno-conversion",
            "-Wno-parentheses-equality", "-Wno-ignored-attributes", "-Wno-for-redefinition"]
    for d in BASE_DEFINES + defines + [stage_define]:
        args += ["-D", d]
    for inc in include_dirs:
        args += ["-I", inc]
    args.append(source_path)
    res = subprocess.run(args, capture_output=True, text=True)
    out = (res.stdout + res.stderr).strip()
    errors = [l for l in out.splitlines() if "error" in l]
    return res.returncode == 0, "\n".join(errors) if errors else out


def main():
    files = sys.argv[1:] or sorted(glob.glob(os.path.join(ROOT, "Assets/_BreathOfEclipse/Shaders/**/*.shader"), recursive=True))
    failures = 0
    checks = 0
    with tempfile.TemporaryDirectory() as tmp:
        inc_root = make_package_root(tmp)
        for path in files:
            text = open(path, encoding="utf-8").read()
            shader_name = re.search(r'Shader\s+"([^"]+)"', text).group(1)
            includes = "\n".join(b for _, b in blocks(text, "HLSLINCLUDE", "ENDHLSL"))
            programs = blocks(text, "HLSLPROGRAM", "ENDHLSL")
            for index, (offset, program) in enumerate(programs):
                pass_name = re.findall(r'Name\s+"([^"]+)"', text[:offset])
                pass_label = pass_name[-1] if pass_name else f"#{index}"
                vert = pragma_value(program, "vertex")
                frag = pragma_value(program, "fragment")
                source = prepare(includes + "\n" + program)
                src_path = os.path.join(tmp, f"{os.path.basename(path)}_{index}.hlsl")
                with open(src_path, "w", encoding="utf-8") as f:
                    f.write(source)
                for defines in variants(program):
                    for profile, entry, stage in (("vs_6_0", vert, "SHADER_STAGE_VERTEX=1"), ("ps_6_0", frag, "SHADER_STAGE_FRAGMENT=1")):
                        if not entry:
                            continue
                        checks += 1
                        ok, msg = compile_stage(DXC, src_path, [inc_root, os.path.dirname(path)], profile, entry, defines, stage)
                        if not ok:
                            failures += 1
                            print(f"FAIL {shader_name} [{pass_label}] {profile} {entry} {defines}\n{msg}\n")
            print(f"checked {shader_name} ({len(programs)} passes)")
    print(f"\n{checks} stage compilations, {failures} failures")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
