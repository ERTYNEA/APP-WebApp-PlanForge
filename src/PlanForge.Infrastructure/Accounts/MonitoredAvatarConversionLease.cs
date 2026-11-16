namespace PlanForge.Infrastructure.Accounts;

using PlanForge.Application.Accounts;
using System.Data.Common;

/// <summary>Links physical reader cancellation to the held session, and stops monitoring before releasing it.</summary>
public sealed class MonitoredAvatarConversionLease : IAvatarConversionLease
{
	private readonly Func<CancellationToken, Task<bool>> check;
	private readonly Func<ValueTask> release;
	private readonly TimeSpan interval;
	private readonly CancellationTokenSource lifetime = new();
	private readonly CancellationTokenSource lost = new();
	private readonly Task monitoring;
	private int disposed;

	public MonitoredAvatarConversionLease(Func<CancellationToken, Task<bool>> check, Func<ValueTask> release, TimeSpan interval)
	{
		this.check = check;
		this.release = release;
		this.interval = interval;
		monitoring = MonitorAsync();
	}

	public CancellationToken LostToken => lost.Token;

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref disposed, 1) != 0)
			return;
		await lifetime.CancelAsync();
		await monitoring;
		try { await release(); }
		finally
		{
			lifetime.Dispose();
			lost.Dispose();
		}
	}

	private async Task MonitorAsync()
	{
		try
		{
			while (!lifetime.IsCancellationRequested)
			{
				await Task.Delay(interval, lifetime.Token);
				if (!await check(lifetime.Token))
				{
					await lost.CancelAsync();
					return;
				}
			}
		}
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
		{
			// Disposal joins the monitor before releasing its owned session.
		}
		catch (Exception exception) when (exception is DbException or TimeoutException or InvalidOperationException or IOException or OperationCanceledException)
		{
			// A failed session probe is loss of exclusion, never evidence that the reader may continue.
			await lost.CancelAsync();
		}
	}
}
