namespace PlanForge.Api.Tests;

using PlanForge.Api.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Application.Planning;
using PlanForge.Infrastructure.Planning;
using System.Net;

public sealed class ProjectQueryFunctionTests
{
	[Fact]
	public async Task ChangesEndpoint_WhenInvoked_UsesKnownGuidOwnerGuardAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var project = await projects.CreateAsync(await fixture.Service.AuthenticateAsync(a.Token), new("Project"));
		var functions = new ProjectQueryFunctions(projects, fixture.Service, fixture.Runner);
		var request = new HttpTestRequest(method: "GET");
		request.Headers.Add("Authorization", "Bearer " + a.Token);
		Assert.Equal(HttpStatusCode.OK, (await functions.ChangesAsync(request, project.Id)).StatusCode);
		var other = new HttpTestRequest(method: "GET");
		other.Headers.Add("Authorization", "Bearer " + b.Token);
		Assert.Equal(HttpStatusCode.NotFound, (await functions.ChangesAsync(other, project.Id)).StatusCode);
	}

	[Fact]
	public async Task SummariesEndpoint_WhenAuthenticated_ReturnsPagedOwnerMetadataAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var functions = new ProjectQueryFunctions(new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System), fixture.Service, fixture.Runner);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.SummariesAsync(new HttpTestRequest(method: "GET"))).StatusCode);
		var request = new HttpTestRequest(method: "GET");
		request.Headers.Add("Authorization", "Bearer " + grant.Token);
		Assert.Equal(HttpStatusCode.OK, (await functions.SummariesAsync(request)).StatusCode);
	}
}
