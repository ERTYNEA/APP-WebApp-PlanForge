namespace PlanForge.Application.Accounts;

public interface IAvatarConverter
{
	Task<byte[]> ConvertAsync(byte[] bytes, string contentType, CancellationToken cancellationToken = default);
}
