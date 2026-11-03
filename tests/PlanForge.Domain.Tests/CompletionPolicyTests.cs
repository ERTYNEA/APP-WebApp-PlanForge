namespace PlanForge.Domain.Tests;

using PlanForge.Domain.Planning;

public sealed class CompletionPolicyTests
{
	[Theory]
	[InlineData(DevelopmentWorkflow.PlanningOnly, CompletionEvidence.WorkDone, false, true)]
	[InlineData(DevelopmentWorkflow.LocalChangesManualApproval, CompletionEvidence.WorkDone, true, false)]
	[InlineData(DevelopmentWorkflow.LocalChangesManualApproval, CompletionEvidence.OwnerApproved, true, true)]
	[InlineData(DevelopmentWorkflow.LocalChangesManualApproval, CompletionEvidence.OwnerApproved, false, false)]
	[InlineData(DevelopmentWorkflow.BranchPullRequest, CompletionEvidence.WorkDone, false, false)]
	[InlineData(DevelopmentWorkflow.BranchPullRequest, CompletionEvidence.MergeConfirmed, false, true)]
	[InlineData(DevelopmentWorkflow.DirectToMain, CompletionEvidence.WorkDone, false, false)]
	[InlineData(DevelopmentWorkflow.DirectToMain, CompletionEvidence.PushConfirmed, false, true)]
	[InlineData((DevelopmentWorkflow)99, CompletionEvidence.WorkDone, true, false)]
	public void CanComplete_WhenEvidenceReported_EnforcesWorkflowAndActor(DevelopmentWorkflow workflow, CompletionEvidence evidence, bool isHuman, bool expected)
	{
		Assert.Equal(expected, CompletionPolicy.CanComplete(workflow, evidence, isHuman));
	}
}
