namespace PlanForge.Application.Accounts;

public sealed record AvatarContent(byte[] Bytes, string ContentType, Guid Version);
