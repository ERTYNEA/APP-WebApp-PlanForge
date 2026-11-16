namespace PlanForge.Domain.Accounts;

public sealed class AvatarOptimizationJob
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid OwnerUserId { get; set; }
	public Guid? AgentId { get; set; }
	public Guid TargetId { get; set; }
	public bool IsAgent { get; set; }
	public Guid Generation { get; set; }
	public long Sequence { get; set; }
	public AvatarJobStatus Status { get; set; }
	public bool IsCurrent { get; set; } = true;
	public int Attempts { get; set; }
	public DateTime AvailableAtUtc { get; set; }
	public DateTime? ReaderUntilUtc { get; set; }
	public Guid? WorkerId { get; set; }
	public long Fence { get; set; }
	public byte[]? RetainedBytes { get; set; }
}
