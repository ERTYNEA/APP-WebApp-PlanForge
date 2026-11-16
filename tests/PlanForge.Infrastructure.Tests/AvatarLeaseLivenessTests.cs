namespace PlanForge.Infrastructure.Tests;

using PlanForge.Infrastructure.Accounts;

public sealed class AvatarLeaseLivenessTests
{
	[Fact]
	public async Task SessionProbeTimeout_CancelsReaderAndDisposesExactlyOnceAsync()
	{
		int released = 0;
		await using var lease = new MonitoredAvatarConversionLease(_ => throw new TimeoutException("Synthetic session timeout"), () => { released++; return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(10));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(Timeout.InfiniteTimeSpan, lease.LostToken).WaitAsync(TimeSpan.FromSeconds(1)));
		await lease.DisposeAsync();
		await lease.DisposeAsync();
		Assert.Equal(1, released);
	}

	[Fact]
	public async Task SessionOwnershipLoss_CancelsReaderBeforeReleasingOwnedSessionAsync()
	{
		int released = 0;
		await using var lease = new MonitoredAvatarConversionLease(_ => Task.FromResult(false), () => { released++; return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(10));
		Assert.True(lease.LostToken.CanBeCanceled);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(Timeout.InfiniteTimeSpan, lease.LostToken).WaitAsync(TimeSpan.FromSeconds(2)));
		Assert.Equal(0, released);
		await lease.DisposeAsync();
		Assert.Equal(1, released);
	}
}
