namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;

internal sealed class WaitingConverter : IAvatarConverter
{
	public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public bool Stopped { get; private set; }
	public async Task<byte[]> ConvertAsync(byte[] bytes, string contentType, CancellationToken cancellationToken = default)
	{
		Started.TrySetResult();
		try
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
			return [];
		}
		finally
		{
			Stopped = true;
		}
	}
}
