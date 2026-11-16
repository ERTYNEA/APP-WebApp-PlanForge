namespace PlanForge.Domain.Accounts;

/// <summary>Byte-free acceptance handshake; a preparing receipt always precedes durable signalling.</summary>
public sealed class AvatarAdmission
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid OwnerUserId { get; set; }
	public Guid? AgentId { get; set; }
	public DateTime ExpiresAtUtc { get; set; }
	public bool Accepted { get; set; }
}
