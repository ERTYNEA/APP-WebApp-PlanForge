namespace PlanForge.Api.Planning;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Planning;

public sealed class PlanFunctions(ProjectService projects, PlanEditor editor, AccountService accounts, HttpEndpointRunner runner)
{
	[Function("Phases")]
	public Task<HttpResponseData> PhasesAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "delete", Route = "v1/projects/{projectId:guid}/phases/{itemId?}")] HttpRequestData request, Guid projectId, string? itemId)
		=> ExecuteItemAsync(request, projectId, "Phase", itemId);

	[Function("Subphases")]
	public Task<HttpResponseData> SubphasesAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "delete", Route = "v1/projects/{projectId:guid}/subphases/{itemId?}")] HttpRequestData request, Guid projectId, string? itemId)
		=> ExecuteItemAsync(request, projectId, "Subphase", itemId);

	[Function("Steps")]
	public Task<HttpResponseData> StepsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "delete", Route = "v1/projects/{projectId:guid}/steps/{itemId?}")] HttpRequestData request, Guid projectId, string? itemId)
		=> ExecuteItemAsync(request, projectId, "Step", itemId);

	[Function("Decisions")]
	public Task<HttpResponseData> DecisionsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "delete", Route = "v1/projects/{projectId:guid}/decisions/{itemId?}")] HttpRequestData request, Guid projectId, string? itemId)
		=> ExecuteItemAsync(request, projectId, "Decision", itemId);

	[Function("CleanCompletedPhases")]
	public Task<HttpResponseData> CleanAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/projects/{projectId:guid}/completed-phases/clean")] HttpRequestData request, Guid projectId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			await projects.GetAsync(actor, projectId, request.FunctionContext.CancellationToken);
			return await editor.CleanCompletedAsync(actor, projectId, await runner.ReadJsonAsync<ConfirmedRevisionRequest>(request), request.FunctionContext.CancellationToken);
		});

	private Task<HttpResponseData> ExecuteItemAsync(HttpRequestData request, Guid projectId, string kind, string? itemId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			ProjectSnapshot project = await projects.GetAsync(actor, projectId, request.FunctionContext.CancellationToken);
			Guid? id = null;
			if (itemId is not null)
			{
				if (!Guid.TryParseExact(itemId, "D", out Guid parsed))
					throw new PlanForgeException(400, "Provide a valid record ID.");
				id = parsed;
				if (!project.Items.Any(item => item.Id == id && item.Kind == kind))
					throw new PlanForgeException(404, "Plan record not found.");
			}
			if (request.Method == "GET")
				return id.HasValue ? project.Items.Single(item => item.Id == id) : project.Items.Where(item => item.Kind == kind).ToList();
			if (request.Method == "DELETE" && id.HasValue)
				return await editor.DeleteItemAsync(actor, projectId, kind, id.Value, await runner.ReadJsonAsync<ConfirmedRevisionRequest>(request), request.FunctionContext.CancellationToken);
			if ((request.Method == "POST" && id is null) || (request.Method == "PUT" && id.HasValue))
				return await editor.SaveItemAsync(actor, projectId, kind, id, await runner.ReadJsonAsync<PlanItemCommand>(request), request.FunctionContext.CancellationToken);
			throw new PlanForgeException(405, "Use POST on a collection or PUT/DELETE on a record.");
		}, request.Method == "POST" ? 201 : 200);
}
