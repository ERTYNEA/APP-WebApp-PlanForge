namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Database;
using System.Data.Common;

public sealed class AvatarWakeRecoveryTests
{
	[Theory]
	[InlineData("lock", false)]
	[InlineData("lock", true)]
	[InlineData("claim", false)]
	[InlineData("claim", true)]
	[InlineData("outstanding", false)]
	[InlineData("outstanding", true)]
	public async Task DeliveredInfrastructureFailure_TransfersWakeWithoutAmplificationThenRecoversAsync(string stage, bool timeout)
	{
		await using var fixture = new AvatarFixture();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(admission.Id, ImageTestData.Raster(SkiaSharp.SKEncodedImageFormat.Png), "image/png");
		// Keep the outstanding-check path free of conversion attempts during the outage.
		await using (var database = fixture.CreateDbContext())
			await database.AvatarOptimizationJobs.ExecuteUpdateAsync(setters => setters.SetProperty(job => job.AvailableAtUtc, fixture.Clock.GetUtcNow().UtcDateTime.AddHours(1)));
		Exception failure = timeout ? new TimeoutException("Injected SQL timeout.") : new WakeDatabaseException();
		var factory = new FaultingFactory(fixture);
		var conversionLock = new FaultingLock();
		var broker = new DeliveryBroker();
		var validator = new SkiaAvatarImageValidator();
		var worker = new AvatarWorker(new EfAvatarStore(factory, fixture.Clock), broker, conversionLock, new SkiaAvatarImageConverter(validator), validator);
		await broker.SendAsync(TimeSpan.Zero);
		for (int delivery = 0; delivery < 12; delivery++)
		{
			factory.Calls = 0;
			factory.FailAt = stage == "claim" ? 1 : stage == "outstanding" ? 2 : 0;
			factory.Failure = failure;
			conversionLock.Failure = stage == "lock" ? failure : null;
			Exception? invocationFailure = await broker.DeliverAsync(worker);
			Assert.Null(invocationFailure);
			Assert.Single(broker.Messages);
			Assert.Equal(stage == "lock" ? 0 : delivery + 1, conversionLock.HoldersDisposed);
			await using var database = fixture.CreateDbContext();
			var job = await database.AvatarOptimizationJobs.SingleAsync();
			Assert.Equal(0, job.Attempts);
			Assert.Equal(AvatarJobStatus.Pending, job.Status);
			Assert.True((await database.Users.SingleAsync()).AvatarTemporary);
		}
		factory.FailAt = 0;
		conversionLock.Failure = null;
		fixture.Clock.Advance(TimeSpan.FromHours(1));
		Assert.Null(await broker.DeliverAsync(worker));
		Assert.Empty(broker.Messages);
		await using var recovered = fixture.CreateDbContext();
		var completed = await recovered.AvatarOptimizationJobs.SingleAsync();
		Assert.Equal(AvatarJobStatus.Completed, completed.Status);
		Assert.Equal(1, completed.Attempts);
		Assert.False((await recovered.Users.SingleAsync()).AvatarTemporary);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SuccessorSendFailure_RetainsOriginalWithoutEnteringSqlThenRecoversAsync(bool timeout)
	{
		await using var fixture = new AvatarFixture();
		var factory = new FaultingFactory(fixture);
		var broker = new DeliveryBroker();
		var validator = new SkiaAvatarImageValidator();
		var worker = new AvatarWorker(new EfAvatarStore(factory, fixture.Clock), broker, new FaultingLock(), new SkiaAvatarImageConverter(validator), validator);
		AvatarWakeReceipt original = await broker.SendAsync(TimeSpan.Zero);
		broker.SendFailure = timeout ? new TimeoutException("Injected send timeout.") : new WakeDatabaseException();
		Assert.Same(broker.SendFailure, await broker.DeliverAsync(worker));
		Assert.Equal(original, Assert.Single(broker.Messages));
		Assert.Equal(0, factory.Calls);
		broker.SendFailure = null;
		Assert.Null(await broker.DeliverAsync(worker));
		Assert.Empty(broker.Messages);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NonInfrastructureLockFailure_RemainsVisibleToHostAsync(bool cancelled)
	{
		await using var fixture = new AvatarFixture();
		var broker = new DeliveryBroker();
		var validator = new SkiaAvatarImageValidator();
		Exception failure = cancelled ? new OperationCanceledException() : new InvalidOperationException("Programming defect.");
		var worker = new AvatarWorker(fixture.Store, broker, new FaultingLock { Failure = failure }, new SkiaAvatarImageConverter(validator), validator);
		Assert.Same(failure, await Record.ExceptionAsync(() => worker.DrainAsync()));
		Assert.Single(broker.Messages);
	}

	private sealed class WakeDatabaseException() : DbException("Injected SQL connection failure.");

	private sealed class FaultingFactory(AvatarFixture fixture) : IDbContextFactory<PlanForgeDbContext>
	{
		public int Calls { get; set; }
		public int FailAt { get; set; }
		public Exception? Failure { get; set; }
		public PlanForgeDbContext CreateDbContext()
		{
			if (++Calls == FailAt)
				throw Failure!;
			return fixture.CreateDbContext();
		}
	}

	private sealed class FaultingLock : IAvatarConversionLock
	{
		public Exception? Failure { get; set; }
		public int HoldersDisposed { get; private set; }
		public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken = default)
			=> Failure is null ? Task.FromResult<IAsyncDisposable?>(new Holder(this)) : Task.FromException<IAsyncDisposable?>(Failure);
		private sealed class Holder(FaultingLock owner) : IAsyncDisposable
		{
			public ValueTask DisposeAsync()
			{
				owner.HoldersDisposed++;
				return ValueTask.CompletedTask;
			}
		}
	}

	private sealed class DeliveryBroker : IAvatarWakeSignal
	{
		public HashSet<AvatarWakeReceipt> Messages { get; } = [];
		public Exception? SendFailure { get; set; }
		public Task<AvatarWakeReceipt> SendAsync(TimeSpan delay, CancellationToken cancellationToken = default)
		{
			if (SendFailure is not null)
				return Task.FromException<AvatarWakeReceipt>(SendFailure);
			var receipt = new AvatarWakeReceipt(Guid.NewGuid().ToString(), "test-receipt");
			Messages.Add(receipt);
			return Task.FromResult(receipt);
		}
		public Task DeleteAsync(AvatarWakeReceipt receipt, CancellationToken cancellationToken = default)
		{
			Assert.True(Messages.Remove(receipt));
			return Task.CompletedTask;
		}
		public async Task<Exception?> DeliverAsync(AvatarWorker worker)
		{
			AvatarWakeReceipt input = Messages.First();
			try
			{
				await worker.DrainAsync();
				Messages.Remove(input); // A successful host invocation acknowledges only this input.
				return null;
			}
			catch (Exception exception)
			{
				return exception; // A failed host invocation retains the original for redelivery.
			}
		}
	}
}
