namespace PlanForge.Contracts.Accounts;

public sealed record AccessGrant(string Token, DateTimeOffset ExpiresAt, UserProfile User);