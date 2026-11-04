"""Verify actual same-SHA job results before Web publication."""
import os


def web_prerequisites_pass(tests, changes, database_required, database_result, api_required, api_result):
	return (
		tests == "success" and changes == "success"
		and (database_result == "success" or (not database_required and database_result == "skipped"))
		and (api_result == "success" or (not api_required and api_result == "skipped"))
	)


if __name__ == "__main__":
	assert web_prerequisites_pass(
		os.environ["TEST_RESULT"], os.environ["SCOPE_RESULT"],
		os.environ["DATABASE_REQUIRED"].lower() == "true", os.environ["DATABASE_RESULT"],
		os.environ["API_REQUIRED"].lower() == "true", os.environ["API_RESULT"],
	), "Web publication denied: tests/scope and all required same-SHA deployments must succeed."
	print("Verified same-SHA tests, scope and required database/API deployment prerequisites.")
