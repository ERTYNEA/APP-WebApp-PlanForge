namespace PlanForge.Contracts.Library;

public sealed record ResourceLinkRequest(long Revision, bool Attach, Guid? ProjectId = null, Guid? PhaseId = null, Guid? SubphaseId = null, Guid? StepId = null);
