namespace PlanForge.Contracts.Planning;

public sealed record PlanItem(Guid Id, string Kind, Guid? ParentId, int SortOrder, string Title, string Description = "", string Status = "Pending", string AssignmentMode = "Inherit", Guid? AssignedAgentId = null, string Resolution = "", string? Evidence = null, Guid? CompletedBy = null, DateTimeOffset? CompletedAt = null, long CreationOrder = 0)
{
	/// <summary>Read-only calculated state; Step Status remains its editable underlying work state.</summary>
	private string? effectiveStatus;
	public string EffectiveStatus { get => effectiveStatus ?? Status; init => effectiveStatus = value; }
}
