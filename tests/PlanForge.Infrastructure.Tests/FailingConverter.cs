namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;

internal sealed class FailingConverter : IAvatarConverter
{
	public Task<byte[]> ConvertAsync(byte[] bytes, string contentType, CancellationToken cancellationToken = default)
		=> throw new IOException("Injected conversion failure.");
}
