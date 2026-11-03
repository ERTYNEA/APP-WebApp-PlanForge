namespace PlanForge.Api.Tests;

using PlanForge.Api.Library;
using PlanForge.Application.Accounts;
using PlanForge.Application.Planning;
using PlanForge.Application.Library;
using PlanForge.Infrastructure.Library;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;
using System.Net;
using System.Text;
using System.Text.Json;

public sealed class ResourceFunctionTests
{
	[Fact]
	public async Task ResourceEndpoints_WhenPhaseOnlyUploaded_DownloadsFidelityAndCleansOnlyConfirmedOrphansAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		project = await new PlanEditor(projects).SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid phase = project.Items.Single(item => item.Kind == "Phase").Id;
		var functions = new ResourceFunctions(new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System), new ResourceLimits(), fixture.Service, fixture.Runner);
		var upload = Request(grant.Token, "# Exact bytes; ñ");
		upload.Headers.Add("Content-Type", "text/markdown");
		upload.Headers.Add("X-File-Name", Uri.EscapeDataString("../phase.md"));
		upload.Headers.Add("X-Phase-Id", phase.ToString());
		var response = await functions.ResourcesAsync(upload);
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		response.Body.Position = 0;
		ResourceProfile resource = (await JsonSerializer.DeserializeAsync<ResourceProfile>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
		Assert.True(resource.IsAssigned);
		Assert.Empty(resource.Projects);
		Assert.Equal("phase.md", resource.FileName);
		var downloaded = await functions.DownloadAsync(Request(grant.Token, method: "GET"), resource.Id);
		Assert.Equal(Encoding.UTF8.GetBytes("# Exact bytes; ñ"), ((MemoryStream)downloaded.Body).ToArray());
		Assert.Equal(HttpStatusCode.OK, (await functions.ProjectResourcesAsync(Request(grant.Token, method: "GET"), project.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await functions.PhaseResourcesAsync(Request(grant.Token, method: "GET"), project.Id, phase)).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, (await functions.CleanAsync(Request(grant.Token, JsonSerializer.Serialize(new CleanOrphansRequest(false, [resource.Id]))))).StatusCode);
		var protectedClean = await functions.CleanAsync(Request(grant.Token, JsonSerializer.Serialize(new CleanOrphansRequest(true, [resource.Id]))));
		protectedClean.Body.Position = 0;
		using (JsonDocument result = await JsonDocument.ParseAsync(protectedClean.Body))
			Assert.Equal(0, result.RootElement.GetProperty("deletedCount").GetInt32());
		var detached = await functions.LinkAsync(Request(grant.Token, JsonSerializer.Serialize(new ResourceLinkRequest(resource.Revision, false, PhaseId: phase))), resource.Id);
		Assert.Equal(HttpStatusCode.OK, detached.StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await functions.CleanAsync(Request(grant.Token, JsonSerializer.Serialize(new CleanOrphansRequest(true, [resource.Id]))))).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await functions.DownloadAsync(Request(grant.Token, method: "GET"), resource.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.ResourcesAsync(new HttpTestRequest(method: "GET"))).StatusCode);
	}

	private static HttpTestRequest Request(string token, string body = "", string method = "POST")
	{
		var request = new HttpTestRequest(body, method);
		request.Headers.Add("Authorization", "Bearer " + token);
		return request;
	}
}
