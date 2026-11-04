namespace PlanForge.Contracts.Accounts;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
