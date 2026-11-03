namespace PlanForge.Contracts.Planning;

public sealed record ProjectSnapshot(Guid Id, long Revision, string Name, string Description, string Status, string Workflow, DateTimeOffset UpdatedAt, IReadOnlyList<PlanItem> Items);
