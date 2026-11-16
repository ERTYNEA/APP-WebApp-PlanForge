namespace PlanForge.Infrastructure.Accounts;

using Microsoft.Extensions.Logging;
using PlanForge.Application.Accounts;
using System.Data.Common;

/// <summary>Drains SQL FIFO only while holding distributed conversion exclusion; the broker is a wake source, not the queue of record.</summary>
public sealed class AvatarWorker(EfAvatarStore store, IAvatarWakeSignal signals, IAvatarConversionLock conversionLock, IAvatarConverter converter, IAvatarImageValidator validator, ILogger<AvatarWorker>? logger = null)
{
	public async Task DrainAsync(CancellationToken cancellationToken = default)
	{
		// Create persistent recovery BEFORE reading SQL or contending for a lease. Any crash retains this successor.
		AvatarWakeReceipt successor = await signals.SendAsync(TimeSpan.FromSeconds(10), cancellationToken);
		try
		{
			if (await DrainCoreAsync(cancellationToken))
				await signals.DeleteAsync(successor, cancellationToken);
		}
		catch (Exception exception) when (exception is DbException or TimeoutException && !cancellationToken.IsCancellationRequested)
		{
			// The acknowledged successor now owns recovery. Complete this invocation so the host
			// does not retain both its input and a new successor on every infrastructure failure.
			logger?.LogWarning("Avatar SQL drain deferred to durable successor. FailureType={FailureType}", exception.GetType().Name);
		}
	}

	/// <summary>The Functions host retains its exact delivered message until this task succeeds; never multiply it with successors.</summary>
	public async Task DrainQueuedAsync(CancellationToken cancellationToken = default)
	{
		if (!await DrainCoreAsync(cancellationToken))
			throw new AvatarWakeDeferredException();
	}

	private async Task<bool> DrainCoreAsync(CancellationToken cancellationToken)
	{
		await using IAsyncDisposable? exclusion = await conversionLock.TryAcquireAsync(cancellationToken);
		if (exclusion is null)
			return false;
		CancellationToken lostSession = (exclusion as IAvatarConversionLease)?.LostToken ?? CancellationToken.None;
		Guid workerId = Guid.NewGuid();
		var elapsed = System.Diagnostics.Stopwatch.StartNew();
		while (!cancellationToken.IsCancellationRequested && !lostSession.IsCancellationRequested && elapsed.Elapsed < TimeSpan.FromSeconds(20))
		{
			AvatarWork? work = await store.ClaimAsync(workerId, cancellationToken);
			if (work is null)
				break;
			using var iteration = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lostSession);
			iteration.CancelAfter(TimeSpan.FromSeconds(20));
			Task heartbeat = MonitorAsync(work, iteration);
			try
			{
				byte[] optimized = await converter.ConvertAsync(work.Bytes, work.ContentType, iteration.Token);
				iteration.Token.ThrowIfCancellationRequested();
				validator.Validate(optimized, "image/webp");
				await store.CompleteAsync(work, optimized, cancellationToken);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				logger?.LogInformation("Avatar conversion cancelled. JobId={JobId}", work.JobId);
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				logger?.LogWarning("Avatar conversion attempt failed. JobId={JobId} FailureType={FailureType}", work.JobId, exception.GetType().Name);
				await store.FailAsync(work, cancellationToken);
			}
			finally
			{
				iteration.Cancel();
				try { await heartbeat; } catch (OperationCanceledException) when (iteration.IsCancellationRequested) { }
				await store.ReleaseAsync(work, CancellationToken.None);
			}
		}
		return !await store.HasOutstandingAsync(cancellationToken);
	}

	private async Task MonitorAsync(AvatarWork work, CancellationTokenSource iteration)
	{
		while (!iteration.IsCancellationRequested)
		{
			await Task.Delay(100, iteration.Token);
			if (!await store.RenewAsync(work, iteration.Token))
			{
				iteration.Cancel();
				return;
			}
		}
	}
}
