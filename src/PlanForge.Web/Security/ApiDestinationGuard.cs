namespace PlanForge.Web.Security;

/// <summary>Checks the final resolved URI before the HTTP transport can send any credential.</summary>
public sealed class ApiDestinationGuard(ApiEndpointPolicy policy) : DelegatingHandler
{
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		if (!policy.Allows(request.RequestUri))
			throw new InvalidOperationException("The PlanForge API request destination is not allowed.");
		return base.SendAsync(request, cancellationToken);
	}
}
