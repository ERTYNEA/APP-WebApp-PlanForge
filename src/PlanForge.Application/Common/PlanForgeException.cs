namespace PlanForge.Application.Common;

public sealed class PlanForgeException(int statusCode, string message) : Exception(message)
{
	public int StatusCode { get; } = statusCode;
}