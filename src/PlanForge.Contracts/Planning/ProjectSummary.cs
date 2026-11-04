namespace PlanForge.Contracts.Planning;

public sealed record ProjectSummary(Guid Id, long Revision, string Name, string Description, string Status, string Workflow, DateTimeOffset UpdatedAt, int PhaseCount, int StepCount, int CompletedSteps, int PendingDecisions, int Progress, long LatestChangeId);
