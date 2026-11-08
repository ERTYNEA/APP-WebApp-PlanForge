namespace PlanForge.Api.Planning;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using System.Globalization;
using System.Web;

public sealed class ProjectQueryFunctions(ProjectService projects, AccountService accounts, HttpEndpointRunner runner)
{
	[Function("WorkspaceDashboard")]
	public Task<HttpResponseData> DashboardAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/dashboard")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			return await projects.GetDashboardAsync(actor, request.FunctionContext.CancellationToken);
		});

	[Function("ProjectChanges")]
	public Task<HttpResponseData> ChangesAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/changes")] HttpRequestData request, Guid projectId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			string? value = HttpUtility.ParseQueryString(request.Url.Query)["after"];
			long after = value is null ? 0 : long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) ? number : throw new PlanForgeException(400, "Use a valid change cursor.");
			return await projects.GetChangesAsync(actor, projectId, after, request.FunctionContext.CancellationToken);
		});

	[Function("ProjectSummaries")]
	public Task<HttpResponseData> SummariesAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/summary")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			var query = HttpUtility.ParseQueryString(request.Url.Query);
			return await projects.ListSummariesAsync(actor, ReadInt(query["skip"], 0), ReadInt(query["take"], 50), request.FunctionContext.CancellationToken);
		});

	private static int ReadInt(string? value, int defaultValue)
		=> value is null ? defaultValue : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : throw new PlanForgeException(400, "Use a valid unsigned page number.");
}
