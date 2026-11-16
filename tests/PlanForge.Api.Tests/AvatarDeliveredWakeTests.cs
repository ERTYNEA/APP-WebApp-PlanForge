namespace PlanForge.Api.Tests;

using PlanForge.Api.Avatars;
using PlanForge.Application.Accounts;
using PlanForge.Infrastructure.Accounts;
using System.Data.Common;

public sealed class AvatarDeliveredWakeTests
{
	[Fact]
	public async Task DeliveredWake_WhenSqlFailsRetainsHostInputWithoutCreatingAnotherSignalAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var signals = new TestAvatarSignal();
		var validator = new SkiaAvatarImageValidator();
		var worker = new AvatarWorker(new EfAvatarStore(fixture, TimeProvider.System), signals, new FailedLock(), new SkiaAvatarImageConverter(validator), validator);
		var functions = new AvatarWakeFunctions(worker);
		await Assert.ThrowsAsync<ProbeDatabaseException>(() => functions.WakeAsync("wake", new HttpTestRequest().FunctionContext));
		Assert.Equal(0, signals.Count);
	}

	private sealed class ProbeDatabaseException : DbException { }
	private sealed class FailedLock : IAvatarConversionLock
	{
		public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken = default) => throw new ProbeDatabaseException();
	}
}
