namespace PlanForge.Domain.Accounts;

public sealed class UserAccount
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public long SecurityVersion { get; set; }
	public byte[]? AvatarBytes { get; set; }
	public string? AvatarContentType { get; set; }
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public string DisplayName { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public string Language { get; set; } = "en";
	public string NormalizedEmail { get; set; } = string.Empty;
	public string PasswordHash { get; set; } = string.Empty;
}
