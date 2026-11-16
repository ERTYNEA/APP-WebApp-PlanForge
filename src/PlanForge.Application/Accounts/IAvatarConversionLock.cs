namespace PlanForge.Application.Accounts;

/// <summary>System-wide conversion exclusion, separate from short admission transactions.</summary>
public interface IAvatarConversionLock
{
	Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken = default);
}
