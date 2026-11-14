namespace PlanForge.Domain.Planning;

/// <summary>A typed, relational plan record. Parentage is validated against the complete project snapshot.</summary>
public sealed class WorkItem
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public long CreationOrder { get; set; }
	public Guid? AssignedAgentId { get; set; }
	public AssignmentMode AssignmentMode { get; set; }
	public Guid? CompletedBy { get; set; }
	public DateTimeOffset? CompletedAt { get; set; }
	public string Description { get; set; } = string.Empty;
	public DateTimeOffset? EffectiveStatusChangedAt { get; set; }
	public CompletionEvidence? Evidence { get; set; }
	public WorkItemKind Kind { get; set; }
	public WorkStatus? ManualStatus { get; set; }
	public Guid? ParentId { get; set; }
	public Guid ProjectId { get; set; }
	public string Resolution { get; set; } = string.Empty;
	public int SortOrder { get; set; }
	public WorkStatus Status { get; set; }
	public string Title { get; set; } = string.Empty;
	public DateTimeOffset UpdatedAt { get; set; }
}
