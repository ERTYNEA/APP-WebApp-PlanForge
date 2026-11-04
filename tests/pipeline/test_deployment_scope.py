"""Regression coverage for deployment scope after failed previous pushes."""

import importlib.util
import pathlib
import unittest

module_path = pathlib.Path(__file__).resolve().parents[2] / "scripts" / "deployment-scope.py"
spec = importlib.util.spec_from_file_location("deployment_scope", module_path)
assert spec is not None and spec.loader is not None
scope = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scope)


class DeploymentScopeTests(unittest.TestCase):
	def test_web_only_push_does_not_republish_api_or_database(self):
		self.assertEqual({"database": False, "api": False, "web": True}, scope.detect_scope(["src/PlanForge.Web/Components/Pages/Home.razor"]))

	def test_contract_and_version_changes_deploy_both_consumers(self):
		for path in ["src/PlanForge.Contracts/ProductVersion.cs", "Directory.Build.props", "global.json"]:
			with self.subTest(path=path):
				self.assertEqual({"database": True, "api": True, "web": True}, scope.detect_scope([path]))

	def test_web_workflow_changes_publish_web_only(self):
		self.assertEqual({"database": False, "api": False, "web": True}, scope.detect_scope([".github/workflows/deploy-web.yml"]))

	def test_api_only_push_reconciles_migrations_left_by_previous_failed_push(self):
		result = scope.detect_scope(["src/PlanForge.Api/Accounts/AccountFunctions.cs"])
		self.assertTrue(result["api"])
		self.assertTrue(result["database"])

	def test_documentation_only_push_does_not_deploy(self):
		self.assertEqual({"database": False, "api": False, "web": False}, scope.detect_scope(["README.md", "docs/api.md"]))

	def test_shared_contract_change_reconciles_database_before_api(self):
		self.assertTrue(scope.detect_scope(["src/PlanForge.Contracts/ProductVersion.cs"])["database"])

	def test_migration_only_change_reconciles_database(self):
		self.assertTrue(scope.detect_scope(["src/PlanForge.Infrastructure/Migrations/InitialAccounts.cs"])["database"])


if __name__ == "__main__":
	unittest.main()