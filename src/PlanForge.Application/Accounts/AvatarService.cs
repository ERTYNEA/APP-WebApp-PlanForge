namespace PlanForge.Application.Accounts;

using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;

/// <summary>Accepts an avatar only after a persistent wake acknowledgement, never using fire-and-forget work.</summary>
public sealed class AvatarService(IAvatarStore store, IAvatarWakeSignal signals, IAvatarImageValidator imageValidator)
{
	public async Task<AvatarState> SetAsync(AuthenticatedActor actor, Guid? agentId, byte[]? bytes, string? contentType, CancellationToken cancellationToken = default)
	{
		if (!actor.IsHuman)
			throw new PlanForgeException(403, "This operation requires a human account session.");
		if (bytes is not null)
			imageValidator.Validate(bytes, contentType ?? string.Empty);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(20));
		var admission = await store.PrepareAsync(actor.UserId, agentId, timeout.Token);
		await signals.SendAsync(TimeSpan.Zero, timeout.Token);
		if (bytes is not null)
			return await store.AcceptAsync(admission.Id, bytes, contentType!.ToLowerInvariant(), timeout.Token);
		AvatarState removed = await store.DeleteAsync(actor.UserId, agentId, timeout.Token, admission.Id);
		var wait = System.Diagnostics.Stopwatch.StartNew();
		while (await store.HasRetainedAsync(actor.UserId, agentId, timeout.Token))
		{
			if (wait.Elapsed > TimeSpan.FromSeconds(5))
				throw new PlanForgeException(409, "Avatar removal is still being confirmed. Please try again shortly.");
			await Task.Delay(50, timeout.Token);
		}
		return removed;
	}
}
