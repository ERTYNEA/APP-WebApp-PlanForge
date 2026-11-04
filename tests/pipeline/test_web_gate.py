"""No skipped required dependency may authorize Web publication."""
import importlib.util
import pathlib
import unittest

path=pathlib.Path(__file__).resolve().parents[2]/"scripts"/"deployment-gates.py"
spec=importlib.util.spec_from_file_location("deployment_gates",path)
assert spec is not None and spec.loader is not None
module=importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class WebGateTests(unittest.TestCase):
	def test_integrated_push_requires_all_successful_prerequisites(self):
		self.assertTrue(module.web_prerequisites_pass("success","success",True,"success",True,"success"))
		for failed in ["failure","cancelled","skipped","pending"]:
			for index in [0,1,3,5]:
				with self.subTest(result=failed,dependency=index):
					values=["success","success",True,"success",True,"success"]
					values[index]=failed
					self.assertFalse(module.web_prerequisites_pass(*values))

	def test_web_only_push_accepts_explicitly_unrequired_skips(self):
		self.assertTrue(module.web_prerequisites_pass("success","success",False,"skipped",False,"skipped"))
		self.assertFalse(module.web_prerequisites_pass("success","success",False,"failure",False,"skipped"))
		self.assertFalse(module.web_prerequisites_pass("success","success",True,"skipped",False,"skipped"))
		self.assertFalse(module.web_prerequisites_pass("success","success",False,"skipped",True,"skipped"))
