namespace PlanForge.Application.Common;

public sealed class PlanForgeException(int statusCode, string message) : Exception(ApplicationText.Get(message))
{
	public int StatusCode { get; } = statusCode;
}
