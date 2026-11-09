namespace PlanForge.Contracts.Library;

public sealed record ResourceUploadRequest(string FileName, string Title, string ContentType, Guid? ProjectId = null, Guid? PhaseId = null, Guid? SubphaseId = null, Guid? StepId = null);
