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
	[Theory]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public async Task UploadNodeHeader_AttachesAtomicallyAndRejectsWrongKindBeforeReadingBytesAsync(string kind)
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("upload-node@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", project.Items.Single(item => item.Kind == "Subphase").Id));
		Guid node = project.Items.Single(item => item.Kind == kind).Id;
		var functions = new ResourceFunctions(new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System), new ResourceLimits(), fixture.Service, fixture.Runner);
		var upload = Request(grant.Token, "Exact bytes");
		upload.Headers.Add("Content-Type", "text/plain");
		upload.Headers.Add("X-File-Name", "node.txt");
		upload.Headers.Add("X-" + kind + "-Id", node.ToString());
		var response = await functions.ResourcesAsync(upload);
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		response.Body.Position = 0;
		ResourceProfile resource = (await JsonSerializer.DeserializeAsync<ResourceProfile>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
		Assert.Equal(node, Assert.Single(resource.Items).ItemId);
		Assert.Empty(resource.Projects);
		Assert.Empty(resource.Phases);
		var invalid = Request(grant.Token, "Do not persist");
		invalid.Headers.Add("X-" + kind + "-Id", project.Items.Single(item => item.Kind == "Phase").Id.ToString());
		Assert.Equal(HttpStatusCode.NotFound, (await functions.ResourcesAsync(invalid)).StatusCode);
		Assert.Single(fixture.Database.Resources);
	}

	[Theory]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public async Task NodeResourceRoutes_ReturnInheritedUnionAndEnforceOwnerAndKindAsync(string kind)
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("nodes@example.test", "CorrectPassword123!", "test"));
		var otherGrant = await fixture.Service.RegisterAsync(new RegisterRequest("other-nodes@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", project.Items.Single(item => item.Kind == "Subphase").Id));
		Guid node = project.Items.Single(item => item.Kind == kind).Id;
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		ResourceProfile resource = await library.UploadAsync(actor, new("root.bin", "Root", "application/octet-stream", project.Id), [1]);
		var functions = new ResourceFunctions(library, new ResourceLimits(), fixture.Service, fixture.Runner);
		System.Reflection.MethodInfo? method = typeof(ResourceFunctions).GetMethod(kind + "ResourcesAsync");
		Assert.NotNull(method);
		async Task<Microsoft.Azure.Functions.Worker.Http.HttpResponseData> InvokeAsync(HttpTestRequest request, Guid id)
			=> await (Task<Microsoft.Azure.Functions.Worker.Http.HttpResponseData>)method.Invoke(functions, [request, project.Id, id])!;
		var response = await InvokeAsync(Request(grant.Token, method: "GET"), node);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		response.Body.Position = 0;
		var resources = await JsonSerializer.DeserializeAsync<List<ResourceProfile>>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
		Assert.Equal(resource.Id, Assert.Single(resources!).Id);
		Assert.Equal(HttpStatusCode.NotFound, (await InvokeAsync(Request(otherGrant.Token, method: "GET"), node)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await InvokeAsync(new HttpTestRequest(method: "GET"), node)).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await InvokeAsync(Request(grant.Token, method: "GET"), Guid.NewGuid())).StatusCode);
		Guid wrongKind = project.Items.First(item => item.Kind != kind).Id;
		Assert.Equal(HttpStatusCode.NotFound, (await InvokeAsync(Request(grant.Token, method: "GET"), wrongKind)).StatusCode);
	}

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
