namespace PlanForge.Api.Accounts;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;

public sealed class AccountFunctions(AccountService accountService, HttpEndpointRunner endpointRunner)
{
	[Function("Logout")]
	public Task<HttpResponseData> LogoutAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/logout")] HttpRequestData request)
		=> endpointRunner.ExecuteAsync(request, async () =>
		{
			await accountService.LogoutAsync(await ReadHumanActorAsync(request), request.FunctionContext.CancellationToken);
			return null;
		}, 204);

	[Function("Session")]
	public Task<HttpResponseData> SessionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/auth/session")] HttpRequestData request)
		=> endpointRunner.ExecuteAsync(request, async () => await accountService.GetProfileAsync(await ReadHumanActorAsync(request), request.FunctionContext.CancellationToken));

	private async Task<AuthenticatedActor> ReadHumanActorAsync(HttpRequestData request)
	{
		string authorization = request.Headers.TryGetValues("Authorization", out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
		if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
			throw new PlanForgeException(401, "A valid access token is required.");
		AuthenticatedActor actor = await accountService.AuthenticateAsync(authorization[7..], request.FunctionContext.CancellationToken);
		if (!actor.IsHuman)
			throw new PlanForgeException(403, "This operation requires a human session.");
		return actor;
	}

	[Function("Login")]
	public Task<HttpResponseData> LoginAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/login")] HttpRequestData request)
		=> endpointRunner.ExecuteAsync(request, async () =>
		{
			endpointRunner.AcquireBudget("auth-login", 60, TimeSpan.FromMinutes(1));
			return await accountService.LoginAsync(await endpointRunner.ReadJsonAsync<LoginRequest>(request), request.FunctionContext.CancellationToken);
		});

	[Function("Register")]
	public Task<HttpResponseData> RegisterAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/register")] HttpRequestData request)
		=> endpointRunner.ExecuteAsync(request, async () =>
		{
			endpointRunner.AcquireBudget("auth-register", 20, TimeSpan.FromHours(1));
			return await accountService.RegisterAsync(await endpointRunner.ReadJsonAsync<RegisterRequest>(request), request.FunctionContext.CancellationToken);
		}, 201);
}
