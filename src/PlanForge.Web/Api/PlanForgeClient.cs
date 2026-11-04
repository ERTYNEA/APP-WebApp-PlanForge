namespace PlanForge.Web.Api;

using Microsoft.AspNetCore.Components.Authorization;
using PlanForge.Web.Security;

public sealed class PlanForgeClient(ApiTransport transport, AuthenticationStateProvider authentication)
{
	public async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, byte[]? bytes = null, string? contentType = null, IReadOnlyDictionary<string,string>? headers = null, CancellationToken cancellationToken = default)
	{
		var user = (await authentication.GetAuthenticationStateAsync()).User;
		string token = user.FindFirst(WebSession.TokenClaim)?.Value ?? throw new ApiException(401, "Sign in to continue.");
		return await transport.SendAsync<T>(method, path, token, body, bytes, contentType, headers, cancellationToken);
	}
	public Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default) => SendAsync<T>(HttpMethod.Get, path, cancellationToken: cancellationToken);
}
