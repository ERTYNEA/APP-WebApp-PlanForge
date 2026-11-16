namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Accounts;

public sealed class AvatarWorkerTests
{
	[Fact]
	public async Task Drain_ActiveDeleteCancelsReaderBeforeConfirmedRemovalAsync()
	{
		await using var fixture = new AvatarFixture();
		var signal = new RecordingSignal();
		var validator = new SkiaAvatarImageValidator();
		var service = new AvatarService(fixture.Store, signal, validator);
		var actor = new AuthenticatedActor(fixture.OwnerId, CredentialKind.HumanSession, Guid.NewGuid(), null);
		await service.SetAsync(actor, null, ImageTestData.Raster(SkiaSharp.SKEncodedImageFormat.Png), "image/png");
		var converter = new WaitingConverter();
		using var host = new CancellationTokenSource();
		Task drain = new AvatarWorker(fixture.Store, signal, new TestConversionLock(), converter, validator).DrainAsync(host.Token);
		try
		{
			await converter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
			Task<PlanForge.Contracts.Accounts.AvatarState> deletion = service.SetAsync(actor, null, null, null);
			Assert.False((await deletion.WaitAsync(TimeSpan.FromSeconds(2))).HasAvatar);
			Assert.True(converter.Stopped);
			await drain.WaitAsync(TimeSpan.FromSeconds(2));
			Assert.False(await fixture.Store.HasRetainedAsync(fixture.OwnerId, null));
		}
		finally
		{
			host.Cancel();
			try { await drain; } catch (OperationCanceledException) { }
		}
	}

	[Fact]
	public async Task Drain_PreparingReceiptRetainsFutureWakeEvenBeforeSqlAcceptanceAsync()
	{
		await using var fixture = new AvatarFixture();
		var signal = new RecordingSignal();
		await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		var validator = new SkiaAvatarImageValidator();
		await new AvatarWorker(fixture.Store, signal, new TestConversionLock(), new SkiaAvatarImageConverter(validator), validator).DrainAsync();
		Assert.Single(signal.Delays);
		Assert.Empty(signal.Deleted);
		Assert.True(await fixture.Store.HasOutstandingAsync());
		fixture.Clock.Advance(TimeSpan.FromSeconds(31));
		Assert.False(await fixture.Store.HasOutstandingAsync());
	}

	[Fact]
	public async Task Drain_FailedAttemptRetainsWakeForBackoffAndEndsWithoutBusyLoopAsync()
	{
		await using var fixture = new AvatarFixture();
		var signal = new RecordingSignal();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(admission.Id, [1], "image/png");
		var validator = new SkiaAvatarImageValidator();
		await new AvatarWorker(fixture.Store, signal, new TestConversionLock(), new FailingConverter(), validator).DrainAsync();
		Assert.Empty(signal.Deleted);
		await using var database = fixture.CreateDbContext();
		var job = await database.AvatarOptimizationJobs.SingleAsync();
		Assert.Equal(AvatarJobStatus.Retry, job.Status);
		Assert.Equal(1, job.Attempts);
		Assert.True(await fixture.Store.HasOutstandingAsync());
	}

	[Fact]
	public async Task Drain_CreatesSuccessorBeforeConversionAndPublishesValidatedWebpAsync()
	{
		await using var fixture = new AvatarFixture();
		var signal = new RecordingSignal();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(admission.Id, ImageTestData.Raster(SkiaSharp.SKEncodedImageFormat.Png), "image/png");
		var validator = new SkiaAvatarImageValidator();
		var converter = new ObservedConverter(signal, new SkiaAvatarImageConverter(validator));
		var worker = new AvatarWorker(fixture.Store, signal, new TestConversionLock(), converter, validator);
		await worker.DrainAsync();
		Assert.Equal(1, converter.Calls);
		Assert.Single(signal.Delays);
		Assert.Single(signal.Deleted);
		await using var database = fixture.CreateDbContext();
		Assert.Equal(AvatarJobStatus.Completed, (await database.AvatarOptimizationJobs.SingleAsync()).Status);
		var user = await database.Users.SingleAsync();
		Assert.Equal("image/webp", user.AvatarContentType);
		Assert.False(user.AvatarTemporary);
		validator.Validate(user.AvatarBytes!, "image/webp");
	}
}

internal sealed class ObservedConverter(RecordingSignal signal, IAvatarConverter converter) : IAvatarConverter
{
	public int Calls { get; private set; }
	public Task<byte[]> ConvertAsync(byte[] bytes, string contentType, CancellationToken cancellationToken = default)
	{
		Assert.NotEmpty(signal.Delays);
		Calls++;
		return converter.ConvertAsync(bytes, contentType, cancellationToken);
	}
}

internal sealed class TestConversionLock : IAvatarConversionLock
{
	private readonly SemaphoreSlim gate = new(1);
	public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken = default)
		=> await gate.WaitAsync(0, cancellationToken) ? new Holder(gate) : null;
	private sealed class Holder(SemaphoreSlim gate) : IAsyncDisposable
	{
		public ValueTask DisposeAsync() { gate.Release(); return ValueTask.CompletedTask; }
	}
}
