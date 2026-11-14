namespace PlanForge.Contracts.Planning;

public sealed record PlanItem(Guid Id, string Kind, Guid? ParentId, int SortOrder, string Title, string Description = "", string Status = "Pending", string AssignmentMode = "Inherit", Guid? AssignedAgentId = null, string Resolution = "", string? Evidence = null, Guid? CompletedBy = null, DateTimeOffset? CompletedAt = null, long CreationOrder = 0)
{
	private string? effectiveStatus;

	/// <summary>Read-only calculated state; Step Status remains its editable underlying work state.</summary>
	public string EffectiveStatus { get => effectiveStatus ?? Status; init => effectiveStatus = value; }

	/// <summary>Read-only server-assigned UTC effective-state entry; unknown legacy dates remain null.</summary>
	public DateTimeOffset? EffectiveStatusChangedAt { get; init; }
}
