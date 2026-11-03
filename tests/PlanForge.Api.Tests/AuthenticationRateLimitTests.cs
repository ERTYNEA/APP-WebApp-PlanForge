namespace PlanForge.Api.Tests;

using System.Net;

public sealed class AuthenticationRateLimitTests
{
	[Fact]
	public async Task LoginAsync_WhenAttemptBudgetExhausted_Returns429Async()
	{
		await using var fixture = new AccountHttpFixture();
		for (var index = 0; index < 60; index++)
			await fixture.Functions.LoginAsync(new HttpTestRequest("{invalid"));

		var response = await fixture.Functions.LoginAsync(new HttpTestRequest("{invalid"));

		Assert.Equal((HttpStatusCode)429, response.StatusCode);
		Assert.Equal("60", response.Headers.GetValues("Retry-After").Single());
	}

	[Fact]
	public async Task RegisterAsync_WhenAttemptBudgetExhausted_Returns429WithoutWritingAsync()
	{
		await using var fixture = new AccountHttpFixture();
		for (var index = 0; index < 20; index++)
			await fixture.Functions.RegisterAsync(new HttpTestRequest("{invalid"));

		var response = await fixture.Functions.RegisterAsync(new HttpTestRequest("{invalid"));

		Assert.Equal((HttpStatusCode)429, response.StatusCode);
		Assert.Empty(fixture.Database.Users);
	}
}
