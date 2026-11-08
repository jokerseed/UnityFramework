#!/usr/bin/env python3
"""Generate isolated .NET projects following engine-free Unity asmdef boundaries.

No Unity stubs and no Assets/ source files are compiled. Logging's two Unity
entry points are the only source exclusions; Events is included in full.
"""
import argparse
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET


SUPPORT = {"Framework.Events", "Framework.Logging"}
LOGGING_ENTRY_POINTS = {"LoggingManager.cs", "LoggingModule.cs"}


def generate(root, output):
    root, output = Path(root).resolve(), Path(output).resolve()
    if output.exists():
        raise ValueError(f"Output already exists; choose a fresh directory: {output}")
    assemblies = {}
    owners = {}
    for path in sorted((root / "Packages").glob("com.jokerseed.framework.*/**/*.asmdef")):
        data = json.loads(path.read_text(encoding="utf-8"))
        name = data["name"]
        if name in assemblies:
            raise ValueError(f"Duplicate assembly name: {name}")
        assemblies[name] = (path, data)
        owners[path.parent] = name
    selected = {name for name, (_, data) in assemblies.items()
                if data.get("noEngineReferences") or name in SUPPORT}
    if not selected:
        raise ValueError("No engine-free assemblies found")
    for name in selected:
        data = assemblies[name][1]
        if data.get("defineConstraints") or data.get("includePlatforms") or data.get("excludePlatforms") or data.get("versionDefines"):
            raise ValueError(f"Conditional assembly needs an explicit consumer mapping: {name}")
    output.mkdir(parents=True)
    sources = {name: [] for name in selected}
    for package in sorted((root / "Packages").glob("com.jokerseed.framework.*")):
        for source in package.rglob("*.cs"):
            owner = next((owners[parent] for parent in source.parents if parent in owners), None)
            if owner not in selected:
                continue
            if owner == "Framework.Logging" and source.name in LOGGING_ENTRY_POINTS:
                continue
            relative = source.relative_to(root)
            target = output / "source" / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            sources[owner].append(target)
    for name in sorted(selected):
        _, data = assemblies[name]
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        props = ET.SubElement(project, "PropertyGroup")
        for key, value in {
            "TargetFramework": "net8.0", "LangVersion": "9.0",
            "AssemblyName": name, "EnableDefaultCompileItems": "false",
            "DisableTransitiveProjectReferences": "true",
            "AllowUnsafeBlocks": str(data.get("allowUnsafeCode", False)).lower(),
            "DefineConstants": "FRAMEWORK_LOG_VERBOSE",
        }.items():
            ET.SubElement(props, key).text = value
        items = ET.SubElement(project, "ItemGroup")
        if not sources[name]:
            raise ValueError(f"Assembly contains no sources: {name}")
        for source in sorted(sources[name]):
            ET.SubElement(items, "Compile", Include="../" + source.relative_to(output).as_posix())
        for reference in data.get("references", []):
            if reference in selected:
                ET.SubElement(items, "ProjectReference", Include=f"../{reference}/{reference}.csproj")
            elif not (name == "Framework.Logging" and reference in {"Framework.Core", "UniTask"}):
                # Logging's Core/UniTask references are used only by the excluded
                # Unity entry points. All other references must remain exact.
                raise ValueError(f"Unsupported assembly dependency: {name} -> {reference}")
        if not data.get("overrideReferences", False):
            ET.SubElement(items, "PackageReference", Include="Newtonsoft.Json", Version="13.0.3")
        destination = output / name / f"{name}.csproj"
        destination.parent.mkdir()
        ET.indent(project)
        ET.ElementTree(project).write(destination, encoding="utf-8", xml_declaration=True)
    consumer = output / "Consumer"
    consumer.mkdir()
    for name in ("GoldenReplayTests.cs", "EventBusTests.cs"):
        shutil.copy2(root / "tests/Framework.Sim.Tests" / name, consumer / name)
    shutil.copy2(root / "tests/Framework.PackageConsumer.Tests/AssemblyBoundaryTests.cs", consumer)
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    props = ET.SubElement(project, "PropertyGroup")
    ET.SubElement(props, "TargetFramework").text = "net8.0"
    ET.SubElement(props, "IsPackable").text = "false"
    ET.SubElement(props, "DisableTransitiveProjectReferences").text = "true"
    items = ET.SubElement(project, "ItemGroup")
    for name in sorted(selected):
        ET.SubElement(items, "ProjectReference", Include=f"../{name}/{name}.csproj")
    for name, version in {"NUnit": "3.14.0", "NUnit3TestAdapter": "4.5.0", "Microsoft.NET.Test.Sdk": "17.9.0"}.items():
        ET.SubElement(items, "PackageReference", Include=name, Version=version)
    ET.indent(project)
    ET.ElementTree(project).write(consumer / "Consumer.csproj", encoding="utf-8", xml_declaration=True)
    print(f"Generated {len(selected)} separate framework assemblies and public-API consumer at {output}")
    return selected


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    generate(args.root, args.output)
