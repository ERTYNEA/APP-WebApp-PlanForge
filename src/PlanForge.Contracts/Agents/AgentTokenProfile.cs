namespace PlanForge.Contracts.Agents;

public sealed record AgentTokenProfile(Guid Id, string Name, Guid? AgentId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, DateTimeOffset? LastUsedAt);
