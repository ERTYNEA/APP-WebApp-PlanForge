namespace PlanForge.Web.Api;

public sealed class ApiException(int statusCode, string message, string? correlationId = null) : Exception(message)
{
	public string? CorrelationId { get; } = correlationId;
	public int StatusCode { get; } = statusCode;
}
