namespace PlanForge.Domain.Agents;

public sealed class Agent
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public byte[]? AvatarBytes { get; set; }
	public string? AvatarContentType { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public string Description { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public string Name { get; set; } = string.Empty;
	public Guid OwnerUserId { get; set; }
}
