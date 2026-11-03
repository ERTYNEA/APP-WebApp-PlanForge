namespace PlanForge.Domain.Accounts;

public sealed class AccessCredential
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid? AgentId { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset ExpiresAt { get; set; }
	public CredentialKind Kind { get; set; }
	public DateTimeOffset? LastUsedAt { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid OwnerUserId { get; set; }
	public DateTimeOffset? RevokedAt { get; set; }
	public string TokenHash { get; set; } = string.Empty;
}
