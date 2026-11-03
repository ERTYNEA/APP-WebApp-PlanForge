namespace PlanForge.Contracts;

public sealed record DownloadFile(byte[] Content, string ContentType, string FileName, bool Inline = false);
