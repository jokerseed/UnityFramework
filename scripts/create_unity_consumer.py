#!/usr/bin/env python3
"""Create a clean seven-package Unity consumer without the demo or commercial Assets."""
import argparse
import json
from pathlib import Path
import shutil


# Git-only UPM dependencies need root manifest overrides. Versions match the repo.
GIT_PACKAGES = {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
    "com.code-philosophy.luban": "https://github.com/focus-creative-games/luban_unity.git#061314200e2f03307c4d90b448e1de7b85431905",
    "com.tuyoogame.yooasset": "https://github.com/tuyoogame/YooAsset.git?path=Assets/YooAsset#3.0.5",
}


def generate(root, output):
    root, output = Path(root).resolve(), Path(output).resolve()
    if output.exists():
        raise ValueError(f"Output already exists: {output}")
    packages = output / "Packages"
    packages.mkdir(parents=True)
    dependencies = dict(GIT_PACKAGES)
    dependencies["com.unity.test-framework"] = "1.1.31"
    # Built-in modules used by the runtime. Other dependencies must be declared
    # by each embedded package, rather than inherited from the demo manifest.
    for name in ("animation", "assetbundle", "audio", "imgui", "jsonserialize",
                 "physics", "physics2d", "ui", "uielements", "unitywebrequest",
                 "unitywebrequestassetbundle", "unitywebrequestaudio",
                 "unitywebrequesttexture"):
        dependencies[f"com.unity.modules.{name}"] = "1.0.0"
    included = []
    for source in sorted((root / "Packages").glob("com.jokerseed.framework.*")):
        if source.name.endswith(".legacy"):
            continue
        metadata = json.loads((source / "package.json").read_text(encoding="utf-8"))
        dependencies[metadata["name"]] = metadata["version"]
        shutil.copytree(source, packages / source.name)
        included.append(metadata["name"])
    (packages / "manifest.json").write_text(json.dumps({"dependencies": dependencies}, indent=2) + "\n", encoding="utf-8")
    settings = output / "ProjectSettings"
    settings.mkdir()
    shutil.copy2(root / "ProjectSettings/ProjectVersion.txt", settings)
    tests = output / "Assets/Tests/Editor"
    tests.mkdir(parents=True)
    shutil.copy2(root / "tests/Framework.PackageConsumer.Tests/AssemblyBoundaryTests.cs", tests)
    shutil.copy2(root / "tests/Framework.Sim.Tests/GoldenReplayTests.cs", tests)
    references = {"Framework.Events", "Framework.Logging", "UnityEngine.TestRunner", "UnityEditor.TestRunner"}
    for path in packages.glob("com.jokerseed.framework.*/**/*.asmdef"):
        assembly = json.loads(path.read_text(encoding="utf-8"))
        if assembly.get("noEngineReferences"):
            references.add(assembly["name"])
    definition = {
        "name": "Framework.PackageConsumer.Tests", "references": sorted(references),
        "includePlatforms": ["Editor"], "overrideReferences": True,
        "precompiledReferences": ["nunit.framework.dll"], "autoReferenced": False,
        "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    }
    (tests / "Framework.PackageConsumer.Tests.asmdef").write_text(json.dumps(definition, indent=2) + "\n", encoding="utf-8")
    print(f"Created clean Unity consumer with {len(included)} packages: {output}")
    return included


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    generate(args.root, args.output)
