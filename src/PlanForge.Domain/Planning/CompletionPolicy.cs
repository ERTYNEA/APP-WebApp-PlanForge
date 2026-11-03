namespace PlanForge.Domain.Planning;

public static class CompletionPolicy
{
	public static bool CanComplete(DevelopmentWorkflow workflow, CompletionEvidence evidence, bool isHuman)
		=> workflow switch
		{
			DevelopmentWorkflow.PlanningOnly => evidence == CompletionEvidence.WorkDone,
			DevelopmentWorkflow.LocalChangesManualApproval => evidence == CompletionEvidence.OwnerApproved && isHuman,
			DevelopmentWorkflow.BranchPullRequest => evidence == CompletionEvidence.MergeConfirmed,
			DevelopmentWorkflow.DirectToMain => evidence == CompletionEvidence.PushConfirmed,
			_ => false
		};
}
