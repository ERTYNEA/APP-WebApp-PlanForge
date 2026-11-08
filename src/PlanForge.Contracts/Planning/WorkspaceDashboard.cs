namespace PlanForge.Contracts.Planning;

public sealed record WorkspaceDashboard(int ProjectCount, int PendingSteps, int CompletedSteps, int TotalSteps, IReadOnlyList<FocusStep> NextSteps)
{
	public int Progress => TotalSteps == 0 ? 0 : (int)(CompletedSteps * 100L / TotalSteps);
}

public sealed record FocusStep(Guid ProjectId, long ProjectRevision, string ProjectName, Guid StepId, string StepName, Guid PhaseId, string PhaseName, Guid SubphaseId, string SubphaseName, string Status, long CreationOrder);
