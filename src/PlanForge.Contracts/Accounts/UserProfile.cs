namespace PlanForge.Contracts.Accounts;

public sealed record UserProfile(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt, bool HasAvatar = false, string Language = "en");
