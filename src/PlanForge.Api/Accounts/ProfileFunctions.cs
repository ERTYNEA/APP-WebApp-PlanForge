namespace PlanForge.Api.Accounts;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Contracts.Accounts;

public sealed class ProfileFunctions(AccountService accounts, AccountManagementService management, HttpEndpointRunner runner)
{
	[Function("UserAvatar")]
	public Task<HttpResponseData> AvatarAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "put", "delete", Route = "v1/users/me/avatar")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts, true);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			if (request.Method == "GET")
				return await management.GetAvatarAsync(actor, request.FunctionContext.CancellationToken);
			byte[]? bytes = request.Method == "DELETE" ? null : await runner.ReadBytesAsync(request, AvatarValidator.MaximumBytes);
			string type = request.Headers.TryGetValues("Content-Type", out IEnumerable<string>? values) ? values.First().Split(';')[0].Trim() : string.Empty;
			return await management.SetAvatarAsync(actor, bytes, type, request.FunctionContext.CancellationToken);
		});

	[Function("ChangePassword")]
	public Task<HttpResponseData> PasswordAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/users/me/password")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts, true);
			runner.AcquireBudget("password:" + actor.UserId, 10, TimeSpan.FromMinutes(1));
			await management.ChangePasswordAsync(actor, await runner.ReadJsonAsync<ChangePasswordRequest>(request), request.FunctionContext.CancellationToken);
			return null;
		}, 204);

	[Function("UserProfile")]
	public Task<HttpResponseData> ProfileAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "put", Route = "v1/users/me")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts, true);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			return request.Method == "GET" ? await accounts.GetProfileAsync(actor, request.FunctionContext.CancellationToken)
				: await management.UpdateAsync(actor, await runner.ReadJsonAsync<UpdateProfileRequest>(request), request.FunctionContext.CancellationToken);
		});
}
