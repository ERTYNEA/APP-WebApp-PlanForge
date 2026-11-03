namespace PlanForge.Api.Tests;

using PlanForge.Contracts.Accounts;
using System.Net;
using System.Text.Json;

public sealed class SessionFunctionTests
{
	[Fact]
	public async Task SessionAsync_WhenNoCredential_ReturnsUnauthorizedAsync()
	{
		await using var fixture = new AccountHttpFixture();

		var response = await fixture.Functions.SessionAsync(new HttpTestRequest(method: "GET"));

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task SessionAsync_WhenValidThenLoggedOut_RecoversProfileThenRejectsSessionAsync()
	{
		await using var fixture = new AccountHttpFixture();
		AccessGrant grant = await fixture.Service.RegisterAsync(new RegisterRequest("test@example.invalid", "Test-only-password-739!", "test"));
		var request = new HttpTestRequest(method: "GET");
		request.Headers.Add("Authorization", "Bearer " + grant.Token);

		var response = await fixture.Functions.SessionAsync(request);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		response.Body.Position = 0;
		UserProfile? profile = await JsonSerializer.DeserializeAsync<UserProfile>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
		Assert.Equal(grant.User.Id, profile?.Id);

		var logout = new HttpTestRequest();
		logout.Headers.Add("Authorization", "Bearer " + grant.Token);
		var logoutResponse = await fixture.Functions.LogoutAsync(logout);
		Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
		Assert.Equal(0, logoutResponse.Body.Length);

		var revoked = await fixture.Functions.SessionAsync(request);
		Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
	}
}
