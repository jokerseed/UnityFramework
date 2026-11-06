"""Packaging checks runnable without Unity or a HybridCLR installation."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT / 'Packages/com.jokerseed.framework.res'

class OptionalHotUpdateTests(unittest.TestCase):
    def test_default_compiled_assemblies_do_not_reference_hybridclr(self):
        for path in (ROOT / 'Packages').rglob('*.asmdef'):
            if any(part.endswith('~') for part in path.parts):
                continue
            self.assertNotIn('HybridCLR.Runtime', json.loads(path.read_text()).get('references', []), str(path))

    def test_adapter_is_opt_in_sample_and_has_typed_dependencies(self):
        package = json.loads((PACKAGE / 'package.json').read_text())
        sample = next(s for s in package['samples'] if s['displayName'] == 'HybridCLR Adapter')
        self.assertTrue(sample['path'].startswith('Samples~/'))
        asm = json.loads((PACKAGE / sample['path'] / 'Framework.HotUpdate.HybridCLR.asmdef').read_text())
        self.assertEqual(['Framework.HotUpdate', 'HybridCLR.Runtime'], asm['references'])
        self.assertNotIn('com.code-philosophy.hybridclr', package['dependencies'])

    def test_base_loader_retains_explicit_missing_backend_error(self):
        code = (PACKAGE / 'Runtime/HotUpdate/HotUpdateLoader.cs').read_text()
        self.assertNotIn('HybridCLR.RuntimeApi', code)
        self.assertIn('throw new NotSupportedException', code)
        self.assertIn('SubsystemRegistration', code)
        sample = (PACKAGE / 'Samples~/HybridCLRAdapter/HybridCLRMetadataBackend.cs').read_text()
        self.assertIn('BeforeSceneLoad', sample)
        self.assertIn('HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly', sample)
