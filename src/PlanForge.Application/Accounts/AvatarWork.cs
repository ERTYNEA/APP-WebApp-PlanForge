namespace PlanForge.Application.Accounts;

/// <summary>An immutable, generation-fenced read held only for one conversion attempt.</summary>
public sealed record AvatarWork(Guid JobId, Guid Generation, Guid WorkerId, long Fence, byte[] Bytes, string ContentType);
