namespace PlanForge.Domain.Planning;

public sealed record ProjectAggregate(Project Project, List<WorkItem> Items);
