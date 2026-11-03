namespace PlanForge.Contracts.Planning;

public sealed record CreateProjectRequest(string Name, string Description = "", string Workflow = "PlanningOnly");
