namespace PlanForge.Domain.Accounts;

public sealed class UserAccount
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public string DisplayName { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public string NormalizedEmail { get; set; } = string.Empty;
	public string PasswordHash { get; set; } = string.Empty;
}