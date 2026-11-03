namespace PlanForge.Domain.Planning;

public enum DevelopmentWorkflow
{
	PlanningOnly,
	LocalChangesManualApproval,
	BranchPullRequest,
	DirectToMain
}