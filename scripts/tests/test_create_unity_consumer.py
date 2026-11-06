import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location(
    "create_unity_consumer", Path(__file__).resolve().parents[1] / "create_unity_consumer.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class UnityConsumerGeneratorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "repo"
        self.output = Path(self.temp.name) / "consumer"
        for name in ("tests/Framework.Sim.Tests/GoldenReplayTests.cs",
                     "tests/Framework.PackageConsumer.Tests/AssemblyBoundaryTests.cs",
                     "ProjectSettings/ProjectVersion.txt", "Assets/ThirdParty/Unwanted.cs"):
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("fixture", encoding="utf-8")
        for suffix in ("core", "battle.legacy"):
            name = "com.jokerseed.framework." + suffix
            path = self.root / "Packages" / name
            path.mkdir(parents=True)
            (path / "package.json").write_text(json.dumps({"name": name, "version": "0.1.0"}), encoding="utf-8")

    def test_consumer_has_only_embedded_packages_and_public_tests(self):
        included = module.generate(self.root, self.output)
        self.assertEqual(["com.jokerseed.framework.core"], included)
        self.assertFalse((self.output / "Assets/ThirdParty").exists())
        self.assertFalse((self.output / "Packages/com.jokerseed.framework.battle.legacy").exists())
        manifest = json.loads((self.output / "Packages/manifest.json").read_text())
        self.assertNotIn("com.coplaydev.unity-mcp", manifest["dependencies"])
        self.assertFalse(any(value.startswith("file:") for value in manifest["dependencies"].values()))
        self.assertTrue((self.output / "Assets/Tests/Editor/GoldenReplayTests.cs").is_file())
        self.assertEqual("fixture", (self.output / "ProjectSettings/ProjectVersion.txt").read_text())

    def test_does_not_overwrite_existing_project(self):
        self.output.mkdir()
        with self.assertRaisesRegex(ValueError, "already exists"):
            module.generate(self.root, self.output)


if __name__ == "__main__":
    unittest.main()
