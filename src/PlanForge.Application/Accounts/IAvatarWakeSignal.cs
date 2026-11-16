namespace PlanForge.Application.Accounts;

/// <summary>A durable wake acknowledgement contains no image bytes or authentication material.</summary>
public interface IAvatarWakeSignal
{
	Task<AvatarWakeReceipt> SendAsync(TimeSpan delay, CancellationToken cancellationToken = default);
	Task DeleteAsync(AvatarWakeReceipt receipt, CancellationToken cancellationToken = default);
}

public sealed record AvatarWakeReceipt(string MessageId, string PopReceipt);
