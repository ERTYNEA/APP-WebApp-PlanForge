namespace PlanForge.Domain.Library;

public sealed class Resource
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public byte[] Content { get; set; } = [];
	public string ContentType { get; set; } = string.Empty;
	public DateTimeOffset CreatedAt { get; set; }
	public string FileName { get; set; } = string.Empty;
	public Guid OwnerUserId { get; set; }
	public long Revision { get; set; } = 1;
	public string Sha256 { get; set; } = string.Empty;
	public long Size { get; set; }
	public string Title { get; set; } = string.Empty;
	public DateTimeOffset UpdatedAt { get; set; }
}
