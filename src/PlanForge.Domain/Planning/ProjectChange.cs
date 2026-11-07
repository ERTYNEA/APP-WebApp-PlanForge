namespace PlanForge.Domain.Planning;

public sealed class ProjectChange
{
	public long Id { get; set; }
	public string Action { get; set; } = string.Empty;
	public string? ActorNameSnapshot { get; set; }
	public Guid ActorId { get; set; }
	public string ActorKind { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
	public Guid ProjectId { get; set; }
	public long Revision { get; set; }
}
