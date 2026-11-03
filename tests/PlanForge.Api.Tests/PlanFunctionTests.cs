namespace PlanForge.Api.Tests;

using PlanForge.Api.Planning;
using PlanForge.Application.Accounts;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;
using System.Net;
using System.Text.Json;

public sealed class PlanFunctionTests
{
	[Fact]
	public async Task PlanEndpoints_WhenAuthenticated_ExecuteHierarchyDecisionClosureAndConfirmedCleanupAsync()
	{
		await using var fixture = new AccountHttpFixture();
		AccessGrant grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		var functions = new PlanFunctions(projects, new PlanEditor(projects), fixture.Service, fixture.Runner);
		var created = await functions.PhasesAsync(Request(grant.Token, new PlanItemCommand(1, "Phase")), project.Id, null);
		Assert.Equal(HttpStatusCode.Created, created.StatusCode);
		project = await projects.GetAsync(actor, project.Id);
		PlanItem phase = project.Items.Single(item => item.Kind == "Phase");
		PlanItem subphase = project.Items.Single(item => item.Kind == "Subphase");
		Assert.Equal(HttpStatusCode.OK, (await functions.SubphasesAsync(Request(grant.Token, null, "GET"), project.Id, subphase.Id.ToString())).StatusCode);
		Assert.Equal(HttpStatusCode.Created, (await functions.StepsAsync(Request(grant.Token, new PlanItemCommand(project.Revision, "Step", subphase.Id, Status: "Completed", Evidence: "WorkDone")), project.Id, null)).StatusCode);
		project = await projects.GetAsync(actor, project.Id);
		Assert.Equal(HttpStatusCode.Created, (await functions.DecisionsAsync(Request(grant.Token, new PlanItemCommand(project.Revision, "Question", phase.Id, Status: "Decided", Resolution: "Resolved")), project.Id, null)).StatusCode);
		project = await projects.GetAsync(actor, project.Id);
		Assert.Equal(HttpStatusCode.OK, (await functions.CleanAsync(Request(grant.Token, new ConfirmedRevisionRequest(project.Revision, true)), project.Id)).StatusCode);
		Assert.Empty((await projects.GetAsync(actor, project.Id)).Items);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.PhasesAsync(new HttpTestRequest(method: "GET"), project.Id, null)).StatusCode);
	}

	private static HttpTestRequest Request(string token, object? body, string method = "POST")
	{
		var request = new HttpTestRequest(body is null ? "" : JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), method);
		request.Headers.Add("Authorization", "Bearer " + token);
		return request;
	}
}
