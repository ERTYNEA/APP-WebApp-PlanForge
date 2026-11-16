namespace PlanForge.Contracts;

public sealed record DownloadFile(byte[] Content, string ContentType, string FileName, bool Inline = false, string? ETag = null)
{
	public long? ProjectChangeId { get; init; }
}
