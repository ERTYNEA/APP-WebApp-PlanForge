namespace PlanForge.Api.Tests;

using PlanForge.Api.Planning;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;
using System.Net;
using System.Text.Json;

public sealed class WorkspaceDashboardFunctionTests
{
	[Fact]
	public async Task Dashboard_AuthenticatesAndReturnsOnlyTheCurrentOwnersBoundedProjectionAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var functions = new ProjectQueryFunctions(projects, fixture.Service, fixture.Runner);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.DashboardAsync(new HttpTestRequest(method: "GET"))).StatusCode);
		var first = await fixture.Service.RegisterAsync(new RegisterRequest("dashboard@example.test", "CorrectPassword123!", "test"));
		var second = await fixture.Service.RegisterAsync(new RegisterRequest("other@example.test", "CorrectPassword123!", "other"));
		await projects.CreateAsync(await fixture.Service.AuthenticateAsync(first.Token), new("Owner project"));
		foreach ((string token, int expectedCount) in new[] { (first.Token, 1), (second.Token, 0) })
		{
			var request = new HttpTestRequest(method: "GET");
			request.Headers.Add("Authorization", "Bearer " + token);
			var response = await functions.DashboardAsync(request);
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			response.Body.Position = 0;
			var result = await JsonSerializer.DeserializeAsync<WorkspaceDashboard>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
			Assert.Equal(expectedCount, result!.ProjectCount);
			Assert.Equal(0, result.Progress);
			Assert.Empty(result.NextSteps);
		}
	}
}
