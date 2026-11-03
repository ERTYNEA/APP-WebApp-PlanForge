namespace PlanForge.Contracts.Planning;

public sealed record PlanItemCommand(long Revision, string Title, Guid? ParentId = null, int? SortOrder = null, string Description = "", string Status = "Pending", string AssignmentMode = "Inherit", Guid? AssignedAgentId = null, string Resolution = "", string? Evidence = null);
