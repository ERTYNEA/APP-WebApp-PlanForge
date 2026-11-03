namespace PlanForge.Contracts.Accounts;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);