import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location(
    "verify_unity_results", Path(__file__).resolve().parents[1] / "verify_unity_results.py"
)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class VerifyUnityResultsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)

    def write(self, xml, name="results.xml"):
        path = self.directory / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(xml, encoding="utf-8")

    def test_accepts_nested_passing_results_and_ignores_coverage(self):
        self.write('<test-run result="Passed"><test-suite><test-case result="Passed"/>\n'
                   '<test-case result="Skipped"/></test-suite></test-run>', "editmode/results.xml")
        self.write('<coverage/>', "coverage.xml")
        self.assertEqual(1, module.verify_results(self.directory))

    def test_rejects_empty_artifact_path(self):
        for path in ("", " "):
            with self.subTest(path=path), self.assertRaises(ValueError):
                module.verify_results(path)

    def test_rejects_missing_directory(self):
        with self.assertRaises(ValueError):
            module.verify_results(self.directory / "missing")

    def test_rejects_absent_results(self):
        with self.assertRaises(ValueError):
            module.verify_results(self.directory)

    def test_rejects_coverage_only(self):
        self.write('<coverage/>')
        with self.assertRaises(ValueError):
            module.verify_results(self.directory)

    def test_rejects_malformed_xml(self):
        self.write('<test-run')
        with self.assertRaises(ValueError):
            module.verify_results(self.directory)

    def test_rejects_zero_or_only_skipped_cases(self):
        for cases in ('', '<test-case result="Skipped"/>'):
            with self.subTest(cases=cases):
                self.write(f'<test-run result="Passed">{cases}</test-run>')
                with self.assertRaises(ValueError):
                    module.verify_results(self.directory)

    def test_rejects_failed_run_even_with_passing_cases(self):
        self.write('<test-run result="Failed"><test-case result="Passed"/></test-run>')
        with self.assertRaises(ValueError):
            module.verify_results(self.directory)

    def test_rejects_failed_case_even_with_passing_run(self):
        self.write('<test-run result="Passed"><test-case result="Passed"/>'
                   '<test-case result="Failed"/></test-run>')
        with self.assertRaises(ValueError):
            module.verify_results(self.directory)

    def test_all_result_files_must_pass(self):
        self.write('<test-run result="Passed"><test-case result="Passed"/></test-run>')
        self.write('<test-run result="Failed"/>', "other.xml")
        with self.assertRaises(ValueError):
            module.verify_results(self.directory)


if __name__ == "__main__":
    unittest.main()
