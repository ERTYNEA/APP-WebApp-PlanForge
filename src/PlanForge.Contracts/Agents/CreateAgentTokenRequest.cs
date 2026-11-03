namespace PlanForge.Contracts.Agents;

public sealed record CreateAgentTokenRequest(string Name, Guid? AgentId = null);
