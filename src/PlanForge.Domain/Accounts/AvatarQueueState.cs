namespace PlanForge.Domain.Accounts;

public sealed class AvatarQueueState
{
	public int Id { get; set; } = 1;
	public long NextSequence { get; set; }
	public Guid? LeaseOwner { get; set; }
	public DateTime? LeaseUntilUtc { get; set; }
	public long Fence { get; set; }
}
