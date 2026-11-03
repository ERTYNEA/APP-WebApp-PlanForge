namespace PlanForge.Contracts.Agents;

public sealed record AgentRequest(string Name, string Description = "", bool IsActive = true);
