namespace PlanForge.Api.Library;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Library;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using System.Globalization;
using System.Web;

public sealed class ResourceFunctions(LibraryService library, ResourceLimits limits, AccountService accounts, HttpEndpointRunner runner)
{
	[Function("Resources")]
	public Task<HttpResponseData> ResourcesAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "v1/resources")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await ReadActorAsync(request);
			if (request.Method == "GET")
			{
				var query = HttpUtility.ParseQueryString(request.Url.Query);
				string assignment = query["assignment"]?.ToLowerInvariant() ?? "all";
				string search = query["search"]?.Trim() ?? string.Empty;
				if (assignment is not ("all" or "assigned" or "orphan") || search.Length > 240)
					throw new PlanForgeException(400, "Choose a valid assignment filter and search term.");
				int skip = ParsePageValue(query["skip"], 0, 0, int.MaxValue);
				int take = ParsePageValue(query["take"], 50, 1, 100);
				List<ResourceProfile> filtered = (await library.ListAsync(actor, request.FunctionContext.CancellationToken))
					.Where(resource => (assignment == "all" || (assignment == "assigned") == resource.IsAssigned)
						&& (resource.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || resource.FileName.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
				return new ResourcePage(filtered.Skip(skip).Take(take).ToList(), filtered.Count, skip, take);
			}
			runner.AcquireBudget("upload:" + actor.UserId, 20, TimeSpan.FromMinutes(1));
			Guid? projectId = ReadOptionalId(request, "X-Project-Id");
			Guid? phaseId = ReadOptionalId(request, "X-Phase-Id");
			Guid? subphaseId = ReadOptionalId(request, "X-Subphase-Id");
			Guid? stepId = ReadOptionalId(request, "X-Step-Id");
			await library.ValidateTargetsAsync(actor, projectId, phaseId, request.FunctionContext.CancellationToken, subphaseId, stepId);
			string fileName = Uri.UnescapeDataString(ReadHeader(request, "X-File-Name"));
			string title = Uri.UnescapeDataString(ReadHeader(request, "X-Resource-Title"));
			if (string.IsNullOrWhiteSpace(title))
				title = LibraryService.SafeFileName(fileName);
			string contentType = ReadHeader(request, "Content-Type").Split(';')[0].Trim();
			if (contentType.Length == 0)
				contentType = "application/octet-stream";
			return await library.UploadAsync(actor, new ResourceUploadRequest(fileName, title, contentType, projectId, phaseId, subphaseId, stepId), await runner.ReadBytesAsync(request, limits.MaximumFileBytes), request.FunctionContext.CancellationToken);
		}, request.Method == "POST" ? 201 : 200);

	[Function("Resource")]
	public Task<HttpResponseData> ResourceAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "put", "delete", Route = "v1/resources/{resourceId:guid}")] HttpRequestData request, Guid resourceId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await ReadActorAsync(request);
			ResourceProfile resource = await library.GetAsync(actor, resourceId, request.FunctionContext.CancellationToken);
			if (request.Method == "GET")
				return resource;
			if (request.Method == "PUT")
				return await library.UpdateAsync(actor, resourceId, await runner.ReadJsonAsync<ResourceMetadataRequest>(request), request.FunctionContext.CancellationToken);
			ConfirmedRevisionRequest command = await runner.ReadJsonAsync<ConfirmedRevisionRequest>(request);
			await library.DeleteAsync(actor, resourceId, command.Revision, command.Confirmed, request.FunctionContext.CancellationToken);
			return null;
		}, request.Method == "DELETE" ? 204 : 200);

	[Function("DownloadResource")]
	public Task<HttpResponseData> DownloadAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/resources/{resourceId:guid}/download")] HttpRequestData request, Guid resourceId)
		=> runner.ExecuteAsync(request, async () => await library.DownloadAsync(await ReadActorAsync(request), resourceId, request.FunctionContext.CancellationToken));

	[Function("ResourceLinks")]
	public Task<HttpResponseData> LinkAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/resources/{resourceId:guid}/links")] HttpRequestData request, Guid resourceId)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await ReadActorAsync(request);
			await library.GetAsync(actor, resourceId, request.FunctionContext.CancellationToken);
			return await library.SetLinkAsync(actor, resourceId, await runner.ReadJsonAsync<ResourceLinkRequest>(request), request.FunctionContext.CancellationToken);
		});

	[Function("CleanOrphanResources")]
	public Task<HttpResponseData> CleanAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/resources/orphans/clean")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await ReadActorAsync(request);
			CleanOrphansRequest command = await runner.ReadJsonAsync<CleanOrphansRequest>(request);
			int count = await library.CleanOrphansAsync(actor, command, request.FunctionContext.CancellationToken);
			return new { deletedCount = count, skippedCount = command.ResourceIds.Count - count };
		});

	[Function("ResourceUsage")]
	public Task<HttpResponseData> UsageAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/resources/usage")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () => await library.UsageAsync(await ReadActorAsync(request), request.FunctionContext.CancellationToken));

	[Function("ProjectResources")]
	public Task<HttpResponseData> ProjectResourcesAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/resources")] HttpRequestData request, Guid projectId)
		=> runner.ExecuteAsync(request, async () => await library.ListProjectAsync(await ReadActorAsync(request), projectId, cancellationToken: request.FunctionContext.CancellationToken));

	[Function("PhaseResources")]
	public Task<HttpResponseData> PhaseResourcesAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/phases/{phaseId:guid}/resources")] HttpRequestData request, Guid projectId, Guid phaseId)
		=> runner.ExecuteAsync(request, async () => await library.ListProjectAsync(await ReadActorAsync(request), projectId, phaseId, request.FunctionContext.CancellationToken));

	[Function("SubphaseResources")]
	public Task<HttpResponseData> SubphaseResourcesAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/subphases/{subphaseId:guid}/resources")] HttpRequestData request, Guid projectId, Guid subphaseId)
		=> runner.ExecuteAsync(request, async () => await library.ListProjectAsync(await ReadActorAsync(request), projectId, subphaseId, request.FunctionContext.CancellationToken, "Subphase"));

	[Function("StepResources")]
	public Task<HttpResponseData> StepResourcesAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/steps/{stepId:guid}/resources")] HttpRequestData request, Guid projectId, Guid stepId)
		=> runner.ExecuteAsync(request, async () => await library.ListProjectAsync(await ReadActorAsync(request), projectId, stepId, request.FunctionContext.CancellationToken, "Step"));

	private static int ParsePageValue(string? value, int fallback, int minimum, int maximum)
	{
		if (value is null)
			return fallback;
		if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed < minimum || parsed > maximum)
			throw new PlanForgeException(400, "Pagination values are outside the allowed range.");
		return parsed;
	}

	private async Task<AuthenticatedActor> ReadActorAsync(HttpRequestData request)
	{
		AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
		runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
		return actor;
	}

	private static string ReadHeader(HttpRequestData request, string name)
		=> request.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;

	private static Guid? ReadOptionalId(HttpRequestData request, string name)
	{
		string value = ReadHeader(request, name);
		if (value.Length == 0)
			return null;
		return Guid.TryParseExact(value, "D", out Guid id) && id != Guid.Empty ? id : throw new PlanForgeException(400, "A target ID header is invalid.");
	}
}
