namespace PlanForge.Contracts.Agents;

public sealed record AgentProfile(Guid Id, string Name, string Description, bool IsActive, bool HasAvatar);
