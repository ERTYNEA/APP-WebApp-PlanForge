namespace PlanForge.Api.Exchange;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Exchange;
using PlanForge.Application.Planning;
using PlanForge.Contracts;

public sealed class WorkPlanFunctions(WorkPlanExchange exchange, ProjectService projects, AccountService accounts, HttpEndpointRunner runner)
{
	[Function("ExportWorkPlan")]
	public Task<HttpResponseData> ExportAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/work-plan/export")] HttpRequestData request, Guid projectId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("exchange:" + actor.UserId, 60, TimeSpan.FromMinutes(1));
			return new DownloadFile(await exchange.ExportAsync(actor, projectId, request.FunctionContext.CancellationToken), "text/csv; charset=utf-8", "work-plan.csv");
		});

	[Function("ImportWorkPlan")]
	public Task<HttpResponseData> ImportAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/projects/{projectId:guid}/work-plan/import")] HttpRequestData request, Guid projectId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("exchange:" + actor.UserId, 60, TimeSpan.FromMinutes(1));
			await projects.GetAsync(actor, projectId, request.FunctionContext.CancellationToken);
			bool replace = request.Headers.TryGetValues("X-PlanForge-Replace", out var values) && values.SingleOrDefault() == "true";
			return await exchange.ImportAsync(actor, projectId, await runner.ReadBytesAsync(request, 1024 * 1024), replace, request.FunctionContext.CancellationToken);
		});
}
