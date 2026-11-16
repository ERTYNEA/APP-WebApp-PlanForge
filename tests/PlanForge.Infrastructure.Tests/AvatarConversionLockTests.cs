namespace PlanForge.Infrastructure.Tests;

using PlanForge.Infrastructure.Accounts;

public sealed class AvatarConversionLockTests
{
	[Fact]
	public async Task SeparateLockObjects_ShareDatabaseExclusionAndReleaseOnDisposeAsync()
	{
		await using var fixture = new AvatarFixture();
		var first = new SqlAvatarConversionLock(fixture);
		var second = new SqlAvatarConversionLock(fixture);
		IAsyncDisposable? lease = await first.TryAcquireAsync();
		Assert.NotNull(lease);
		Assert.Null(await second.TryAcquireAsync());
		await lease.DisposeAsync();
		await using IAsyncDisposable? recovered = await second.TryAcquireAsync();
		Assert.NotNull(recovered);
	}
}
