namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;
using PlanForge.Domain.Accounts;

public sealed class AuthenticationTests
{
	[Fact]
	public async Task LoginAsync_WhenValidPassword_ReturnsNewRecoverableHumanSessionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		AccessGrant original = await fixture.Service.RegisterAsync(new RegisterRequest("test@example.invalid", "Test-only-password-739!", "test"));

		AccessGrant login = await fixture.Service.LoginAsync(new LoginRequest("TEST@EXAMPLE.INVALID", "Test-only-password-739!"));
		fixture.Database.ChangeTracker.Clear();
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(login.Token);

		Assert.NotEqual(original.Token, login.Token);
		Assert.Equal(original.User.Id, actor.UserId);
		Assert.True(actor.IsHuman);
		Assert.Equal(2, fixture.Database.AccessCredentials.Count());
	}

	[Theory]
	[InlineData("test@example.invalid", "wrong-password")]
	[InlineData("unknown@example.invalid", "Test-only-password-739!")]
	public async Task LoginAsync_WhenCredentialsIncorrect_ReturnsSafeUnauthorizedAsync(string email, string password)
	{
		await using var fixture = new AccountDatabaseFixture();
		await fixture.Service.RegisterAsync(new RegisterRequest("test@example.invalid", "Test-only-password-739!", "test"));

		PlanForgeException error = await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.LoginAsync(new LoginRequest(email, password)));

		Assert.Equal(401, error.StatusCode);
		Assert.Equal("Invalid email or password.", error.Message);
	}

	[Fact]
	public async Task LogoutAsync_WhenSessionRevoked_RejectsFutureAuthenticationAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		AccessGrant grant = await fixture.Service.RegisterAsync(new RegisterRequest("test@example.invalid", "Test-only-password-739!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);

		await fixture.Service.LogoutAsync(actor);
		PlanForgeException error = await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(grant.Token));

		Assert.Equal(401, error.StatusCode);
	}

	[Fact]
	public async Task AuthenticateAsync_WhenExpired_RejectsWithReadableExpirationAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		AccessGrant grant = await fixture.Service.RegisterAsync(new RegisterRequest("test@example.invalid", "Test-only-password-739!", "test"));
		AccessCredential credential = fixture.Database.AccessCredentials.Single();
		credential.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
		await fixture.Database.SaveChangesAsync();

		PlanForgeException error = await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(grant.Token));

		Assert.Equal(401, error.StatusCode);
		Assert.Contains("expired", error.Message);
	}
}