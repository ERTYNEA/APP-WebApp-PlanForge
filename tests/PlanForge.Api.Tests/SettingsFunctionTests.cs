namespace PlanForge.Api.Tests;

using PlanForge.Api.Settings;
using PlanForge.Contracts.Accounts;
using System.Net;

public sealed class SettingsFunctionTests
{
	[Fact]
	public async Task SettingsEndpoint_WhenAuthenticated_ExposesSafeLimitsAndTheFourOrderedWorkflowsAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var functions = new SettingsFunctions(fixture.Service, new PlanForge.Application.Library.ResourceLimits(), fixture.Runner);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.SettingsAsync(new HttpTestRequest(method: "GET"))).StatusCode);
		var request = new HttpTestRequest(method: "GET");
		request.Headers.Add("Authorization", "Bearer " + grant.Token);
		var response = await functions.SettingsAsync(request);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		response.Body.Position = 0;
		string body = await new StreamReader(response.Body).ReadToEndAsync();
		Assert.Contains("PlanningOnly", body);
		Assert.Contains("DirectToMain", body);
		Assert.Contains("262144", body);
		Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
	}
}
