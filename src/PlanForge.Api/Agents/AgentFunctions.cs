namespace PlanForge.Api.Agents;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Agents;
using PlanForge.Application.Common;
using PlanForge.Contracts.Agents;

public sealed class AgentFunctions(AgentService agents, AccountService accounts, HttpEndpointRunner runner)
{
	[Function("Agents")]
	public Task<HttpResponseData> AgentsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "v1/agents")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			return request.Method == "GET" ? await agents.ListAsync(actor, request.FunctionContext.CancellationToken)
				: await agents.CreateAsync(actor, await runner.ReadJsonAsync<AgentRequest>(request), request.FunctionContext.CancellationToken);
		}, request.Method == "POST" ? 201 : 200);

	[Function("Agent")]
	public Task<HttpResponseData> AgentAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "put", "delete", Route = "v1/agents/{agentId:guid}")] HttpRequestData request, Guid agentId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			AgentProfile agent = (await agents.ListAsync(actor, request.FunctionContext.CancellationToken)).SingleOrDefault(item => item.Id == agentId) ?? throw new PlanForgeException(404, "Agent not found.");
			if (request.Method == "GET")
				return agent;
			if (request.Method == "DELETE")
			{
				if (!actor.IsHuman)
					throw new PlanForgeException(403, "Only a human account session may delete an agent.");
				DeleteAgentRequest confirmation = await runner.ReadJsonAsync<DeleteAgentRequest>(request);
				if (!confirmation.Confirmed)
					throw new PlanForgeException(400, "Confirm permanent agent deletion.");
				await agents.DeleteAsync(actor, agentId, request.FunctionContext.CancellationToken);
				return null;
			}
			AgentRequest command = await runner.ReadJsonAsync<AgentRequest>(request);
			return await agents.UpdateAsync(actor, agentId, command, request.FunctionContext.CancellationToken);
		}, request.Method == "DELETE" ? 204 : 200);

	[Function("AgentTokens")]
	public Task<HttpResponseData> TokensAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "v1/agent-tokens")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts, true);
			runner.AcquireBudget("tokens:" + actor.UserId, 60, TimeSpan.FromMinutes(1));
			return request.Method == "GET" ? await agents.ListTokensAsync(actor, request.FunctionContext.CancellationToken)
				: await agents.CreateTokenAsync(actor, await runner.ReadJsonAsync<CreateAgentTokenRequest>(request), request.FunctionContext.CancellationToken);
		}, request.Method == "POST" ? 201 : 200);

	[Function("CleanAgentTokens")]
	public Task<HttpResponseData> CleanTokensAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/agent-tokens/history/clean")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts, true);
			runner.AcquireBudget("tokens:" + actor.UserId, 60, TimeSpan.FromMinutes(1));
			int deletedCount = await agents.CleanTokensAsync(actor, await runner.ReadJsonAsync<CleanAgentTokensRequest>(request), request.FunctionContext.CancellationToken);
			return new { deletedCount };
		});

	[Function("RevokeAgentToken")]
	public Task<HttpResponseData> RevokeAsync([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/agent-tokens/{tokenId:guid}")] HttpRequestData request, Guid tokenId)
		=> runner.ExecuteAsync(request, async () =>
		{
			await agents.RevokeTokenAsync(await RequestActorReader.ReadAsync(request, accounts, true), tokenId, request.FunctionContext.CancellationToken);
			return null;
		}, 204);

	[Function("RenewAgentToken")]
	public Task<HttpResponseData> RenewAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/agent-tokens/{tokenId:guid}/renew")] HttpRequestData request, Guid tokenId)
		=> runner.ExecuteAsync(request, async () => await agents.RenewTokenAsync(await RequestActorReader.ReadAsync(request, accounts, true), tokenId, request.FunctionContext.CancellationToken));
}
