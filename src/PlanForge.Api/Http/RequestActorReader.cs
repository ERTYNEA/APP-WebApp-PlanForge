namespace PlanForge.Api.Http;

using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;

public static class RequestActorReader
{
	public static async Task<AuthenticatedActor> ReadAsync(HttpRequestData request, AccountService accounts, bool humanOnly = false)
	{
		string authorization = request.Headers.TryGetValues("Authorization", out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
		if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
			throw new PlanForgeException(401, "A valid access token is required.");
		AuthenticatedActor actor = await accounts.AuthenticateAsync(authorization[7..], request.FunctionContext.CancellationToken);
		if (humanOnly && !actor.IsHuman)
			throw new PlanForgeException(403, "This operation requires a human session.");
		return actor;
	}
}
