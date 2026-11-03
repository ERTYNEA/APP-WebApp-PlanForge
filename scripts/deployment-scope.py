"""Determine trusted-main deployment work; no credentials or network access."""

import os
import subprocess


def detect_scope(changed):
	database = any(path.startswith(("src/PlanForge.Infrastructure/Migrations/", "src/PlanForge.Infrastructure/Database/")) or path == ".github/workflows/deploy-database.yml" for path in changed)
	api = any(path.startswith(("src/PlanForge.Api/", "src/PlanForge.Application/", "src/PlanForge.Contracts/", "src/PlanForge.Domain/", "src/PlanForge.Infrastructure/")) or path in {"global.json", "Directory.Build.props", ".github/workflows/deploy-api.yml"} for path in changed)
	# Every API publication reconciles pending migrations, including those from earlier failed pushes.
	return {"database": database or api, "api": api}


if __name__ == "__main__":
	before = os.environ.get("BEFORE", "")
	if os.environ.get("EVENT") == "workflow_dispatch":
		changed = ["src/PlanForge.Infrastructure/Migrations/manual", "src/PlanForge.Api/manual"]
	elif before and set(before) != {"0"}:
		changed = subprocess.check_output(["git", "diff", "--name-only", before, "HEAD"], text=True).splitlines()
	else:
		changed = subprocess.check_output(["git", "diff-tree", "--root", "--no-commit-id", "--name-only", "-r", "HEAD"], text=True).splitlines()
	with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
		for name, enabled in detect_scope(changed).items():
			output.write(f"{name}={str(enabled).lower()}\n")