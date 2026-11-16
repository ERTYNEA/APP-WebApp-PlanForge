namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Infrastructure.Accounts;

public sealed class AvatarWorkerLeaseLossTests
{
	[Fact]
	public async Task LostSession_CancelsReaderBeforeReleasingWorkAndKeepsDurableRecoveryAsync()
	{
		await using var fixture = new AvatarFixture();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(admission.Id, [1], "image/png");
		var signal = new RecordingSignal();
		var converter = new WaitingConverter();
		using var exclusion = new LosingLock();
		using var host = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		Task drain = new AvatarWorker(fixture.Store, signal, exclusion, converter, new SkiaAvatarImageValidator()).DrainAsync(host.Token);
		try
		{
			await converter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
			exclusion.Lost.Cancel();
			await drain.WaitAsync(TimeSpan.FromSeconds(1));
			Assert.True(converter.Stopped);
			Assert.Empty(signal.Deleted);
			Assert.True(await fixture.Store.HasOutstandingAsync());
		}
		finally
		{
			host.Cancel();
			try { await drain; } catch (OperationCanceledException) when (host.IsCancellationRequested) { }
		}
	}

	private sealed class LosingLock : IAvatarConversionLock, IDisposable
	{
		public CancellationTokenSource Lost { get; } = new();
		public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken = default) => Task.FromResult<IAsyncDisposable?>(new Lease(Lost.Token));
		public void Dispose() => Lost.Dispose();
		private sealed record Lease(CancellationToken LostToken) : IAvatarConversionLease
		{
			public ValueTask DisposeAsync() => ValueTask.CompletedTask;
		}
	}
}
