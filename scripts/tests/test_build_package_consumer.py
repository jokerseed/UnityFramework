import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location(
    "build_package_consumer", Path(__file__).resolve().parents[1] / "build_package_consumer.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PackageConsumerGeneratorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "repo"
        self.output = Path(self.temp.name) / "consumer"
        for name in ("Framework.Sim.Tests/GoldenReplayTests.cs",
                     "Framework.PackageConsumer.Tests/AssemblyBoundaryTests.cs"):
            path = self.root / "tests" / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("// fixture", encoding="utf-8")

    def assembly(self, directory, name, references=(), **kwargs):
        path = self.root / "Packages/com.jokerseed.framework.sample" / directory
        path.mkdir(parents=True, exist_ok=True)
        data = {"name": name, "references": list(references), "noEngineReferences": True}
        data.update(kwargs)
        (path / f"{name}.asmdef").write_text(json.dumps(data), encoding="utf-8")
        (path / f"{name}.cs").write_text("// fixture", encoding="utf-8")

    def test_nested_assembly_sources_do_not_leak_into_parent(self):
        self.assembly("Runtime", "Parent", ["Child"])
        self.assembly("Runtime/Nested", "Child")
        self.assembly("Runtime/Unity", "UnityOnly", noEngineReferences=False)
        module.generate(self.root, self.output)
        parent = ET.parse(self.output / "Parent/Parent.csproj")
        includes = [node.get("Include") for node in parent.findall(".//Compile")]
        self.assertEqual(1, len(includes))
        self.assertTrue(includes[0].endswith("Parent.cs"))
        references = parent.findall(".//ProjectReference")
        self.assertEqual(["../Child/Child.csproj"], [node.get("Include") for node in references])
        self.assertEqual("true", parent.findtext(".//DisableTransitiveProjectReferences"))
        self.assertFalse((self.output / "UnityOnly").exists())
        self.assertFalse(any(path.name == "UnityOnly.cs" for path in self.output.rglob("*.cs")))

    def test_duplicate_names_are_rejected(self):
        self.assembly("A", "Same")
        self.assembly("B", "Same")
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            module.generate(self.root, self.output)

    def test_unknown_dependency_is_rejected(self):
        self.assembly("A", "A", ["Unknown"])
        with self.assertRaisesRegex(ValueError, "Unsupported assembly dependency"):
            module.generate(self.root, self.output)

    def test_conditional_assembly_is_not_silently_misrepresented(self):
        self.assembly("A", "A", defineConstraints=["SPECIAL_PLATFORM"])
        with self.assertRaisesRegex(ValueError, "Conditional assembly"):
            module.generate(self.root, self.output)

    def test_existing_output_is_not_overwritten(self):
        self.assembly("A", "A")
        self.output.mkdir()
        with self.assertRaisesRegex(ValueError, "already exists"):
            module.generate(self.root, self.output)

    def test_logging_adapter_cannot_hide_new_dependencies(self):
        self.assembly("Logging", "Framework.Logging", ["UndeclaredIntegration"], noEngineReferences=False)
        with self.assertRaisesRegex(ValueError, "Unsupported assembly dependency"):
            module.generate(self.root, self.output)


if __name__ == "__main__":
    unittest.main()
