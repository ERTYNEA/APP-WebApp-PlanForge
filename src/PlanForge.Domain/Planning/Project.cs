namespace PlanForge.Domain.Planning;

public sealed class Project
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateTimeOffset CreatedAt { get; set; }
	public string Description { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public Guid OwnerUserId { get; set; }
	public long Revision { get; set; } = 1;
	public int SortOrder { get; set; }
	public WorkStatus Status { get; set; }
	public DateTime UpdatedAt { get; set; }
	public DevelopmentWorkflow Workflow { get; set; }
}
