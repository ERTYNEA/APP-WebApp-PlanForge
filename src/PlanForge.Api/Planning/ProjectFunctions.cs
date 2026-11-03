namespace PlanForge.Api.Planning;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Planning;

public sealed class ProjectFunctions(ProjectService projects, AccountService accounts, HttpEndpointRunner runner)
{
	[Function("Projects")]
	public Task<HttpResponseData> CollectionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "v1/projects")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			return request.Method == "GET"
				? await projects.ListAsync(actor, request.FunctionContext.CancellationToken)
				: await projects.CreateAsync(actor, await runner.ReadJsonAsync<CreateProjectRequest>(request), request.FunctionContext.CancellationToken);
		}, request.Method == "POST" ? 201 : 200);

	[Function("Project")]
	public Task<HttpResponseData> DetailAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "put", "delete", Route = "v1/projects/{projectId:guid}")] HttpRequestData request, Guid projectId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			if (request.Method == "GET")
				return await projects.GetAsync(actor, projectId, request.FunctionContext.CancellationToken);
			// Authorize the target before reading a mutation body.
			await projects.GetAsync(actor, projectId, request.FunctionContext.CancellationToken);
			if (request.Method == "PUT")
				return await projects.SaveAsync(actor, projectId, await runner.ReadJsonAsync<ProjectSnapshot>(request), false, request.FunctionContext.CancellationToken);
			if (request.Method != "DELETE")
				throw new PlanForgeException(405, "This method is not supported.");
			ConfirmedRevisionRequest command = await runner.ReadJsonAsync<ConfirmedRevisionRequest>(request);
			await projects.DeleteAsync(actor, projectId, command.Revision, command.Confirmed, request.FunctionContext.CancellationToken);
			return null;
		}, request.Method == "DELETE" ? 204 : 200);
}
