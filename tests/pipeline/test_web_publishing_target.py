"""Publishing metadata validation never establishes credentials by XML alone."""
import importlib.util
import pathlib
import unittest
import xml.etree.ElementTree as ET

path=pathlib.Path(__file__).resolve().parents[2]/"scripts"/"resolve-web-target.py"
spec=importlib.util.spec_from_file_location("web_target",path)
assert spec is not None and spec.loader is not None
module=importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class PublishingTargetTests(unittest.TestCase):
	def profile(self, **overrides):
		root=ET.Element("publishData")
		values={"publishMethod":"MSDeploy","publishUrl":"planforge-web-abcd.scm.spaincentral-01.azurewebsites.net:443","destinationAppUrl":"https://planforge-web-abcd.spaincentral-01.azurewebsites.net","userName":"example-publisher","userPWD":"illustrative-not-a-credential"}
		values.update(overrides)
		ET.SubElement(root,"publishProfile",values)
		return ET.tostring(root,encoding="unicode")

	def test_valid_metadata_preserves_special_characters_without_secret_repr(self):
		target=module.parse_profile(self.profile(userPWD="example&symbols=preserved"),"PlanForge-Web")
		self.assertEqual("https://planforge-web-abcd.spaincentral-01.azurewebsites.net",target.public_url)
		self.assertEqual("example&symbols=preserved",target.password)
		self.assertNotIn(target.password,repr(target))

	def test_missing_blank_redacted_or_foreign_metadata_fails_closed(self):
		for values in [{"userPWD":""},{"userPWD":"REDACTED"},{"userPWD":"   "},{"userName":"REDACTED"},{"destinationAppUrl":"https://foreign.azurewebsites.net"},{"publishUrl":"https://foreign.scm.azurewebsites.net"},{"publishUrl":"http://planforge-web-abcd.scm.spaincentral-01.azurewebsites.net"}]:
			with self.subTest(fields=list(values)):
				with self.assertRaises(ValueError): module.parse_profile(self.profile(**values),"PlanForge-Web")
