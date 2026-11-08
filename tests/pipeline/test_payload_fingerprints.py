import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'scripts' / 'payload-fingerprint.py'


class PayloadFingerprintTests(unittest.TestCase):
    def test_payload_symlinks_are_rejected_before_following_them(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / 'payload'
            root.mkdir()
            (root / 'App.dll').write_bytes(b'controlled assembly')
            outside = Path(directory) / 'outside.txt'
            outside.write_bytes(b'outside payload')
            (root / 'linked.txt').symlink_to(outside)
            result = subprocess.run(['python3', str(SCRIPT), str(root), 'App.dll'], capture_output=True, text=True)
            self.assertNotEqual(0, result.returncode, 'A payload must not follow a link outside its publication root')
            self.assertEqual('', result.stdout)

    def test_missing_entry_assembly_cannot_claim_a_payload_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run(['python3', str(SCRIPT), directory, 'Missing.dll'], capture_output=True, text=True)
            self.assertNotEqual(0, result.returncode)
            self.assertEqual('', result.stdout)

    def test_actual_payload_has_stable_content_based_fingerprint(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'App.dll').write_bytes(b'controlled assembly')
            (root / 'asset.css').write_bytes(b'controlled design')
            result = subprocess.run(['python3', str(SCRIPT), str(root), 'App.dll'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, 'The actual payload fingerprint command must work: ' + result.stderr)
            first = json.loads(result.stdout)
            self.assertEqual(hashlib.sha256(b'controlled assembly').hexdigest(), first.get('assemblySha256'))
            self.assertEqual(2, first.get('files'))
            self.assertEqual(len(b'controlled assemblycontrolled design'), first.get('bytes'))
            second = json.loads(subprocess.check_output(['python3', str(SCRIPT), str(root), 'App.dll']))
            self.assertEqual(first, second)
            (root / 'asset.css').write_bytes(b'changed design')
            changed = json.loads(subprocess.check_output(['python3', str(SCRIPT), str(root), 'App.dll']))
            self.assertNotEqual(first['payloadManifestSha256'], changed['payloadManifestSha256'])
            self.assertEqual(first['assemblySha256'], changed['assemblySha256'])


if __name__ == '__main__':
    unittest.main()
