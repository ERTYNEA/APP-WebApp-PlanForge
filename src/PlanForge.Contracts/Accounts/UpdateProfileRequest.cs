namespace PlanForge.Contracts.Accounts;

public sealed record UpdateProfileRequest(string DisplayName, string Language = "en");
