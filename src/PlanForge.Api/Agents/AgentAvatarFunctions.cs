namespace PlanForge.Api.Agents;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Agents;
using PlanForge.Application.Common;

public sealed class AgentAvatarFunctions(AgentService agents, AccountService accounts, HttpEndpointRunner runner)
{
	[Function("AgentAvatar")]
	public Task<HttpResponseData> AvatarAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "put", "delete", Route = "v1/agents/{agentId:guid}/avatar")] HttpRequestData request, Guid agentId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts, request.Method != "GET");
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			if (request.Method == "GET")
				return await agents.GetAvatarAsync(actor, agentId, request.FunctionContext.CancellationToken);
			if (!(await agents.ListAsync(actor, request.FunctionContext.CancellationToken)).Any(agent => agent.Id == agentId))
				throw new PlanForgeException(404, "Agent not found.");
			byte[]? bytes = request.Method == "DELETE" ? null : await runner.ReadBytesAsync(request, AvatarValidator.MaximumBytes);
			string type = request.Headers.TryGetValues("Content-Type", out IEnumerable<string>? values) ? values.First().Split(';')[0].Trim() : string.Empty;
			return await agents.SetAvatarAsync(actor, agentId, bytes, type, request.FunctionContext.CancellationToken);
		});
}
