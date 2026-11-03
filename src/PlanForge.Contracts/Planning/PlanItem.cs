namespace PlanForge.Contracts.Planning;

public sealed record PlanItem(Guid Id, string Kind, Guid? ParentId, int SortOrder, string Title, string Description = "", string Status = "Pending", string AssignmentMode = "Inherit", Guid? AssignedAgentId = null, string Resolution = "", string? Evidence = null, Guid? CompletedBy = null, DateTimeOffset? CompletedAt = null);
