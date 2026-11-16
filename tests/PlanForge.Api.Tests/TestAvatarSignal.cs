namespace PlanForge.Api.Tests;

using PlanForge.Application.Accounts;

public sealed class TestAvatarSignal : IAvatarWakeSignal
{
	public int Count { get; private set; }
	public Task<AvatarWakeReceipt> SendAsync(TimeSpan delay, CancellationToken cancellationToken = default)
	{
		Count++;
		return Task.FromResult(new AvatarWakeReceipt(Guid.NewGuid().ToString(), "isolated-http-receipt"));
	}
	public Task DeleteAsync(AvatarWakeReceipt receipt, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
