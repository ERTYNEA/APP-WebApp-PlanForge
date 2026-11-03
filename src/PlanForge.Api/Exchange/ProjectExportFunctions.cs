namespace PlanForge.Api.Exchange;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Exchange;

public sealed class ProjectExportFunctions(ProjectArchiveExporter exporter, AccountService accounts, HttpEndpointRunner runner)
{
	[Function("ExportProjectArchive")]
	public Task<HttpResponseData> ExportAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/export")] HttpRequestData request, Guid projectId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("archive:" + actor.UserId, 10, TimeSpan.FromMinutes(1));
			return await exporter.ExportAsync(actor, projectId, request.FunctionContext.CancellationToken);
		});
}
