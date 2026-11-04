namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Agents;
using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;

public sealed class CredentialIssuanceRaceTests
{
	[Fact]
	public async Task LoginAsync_WhenPasswordChangesAfterOldPasswordVerification_NewlyInsertedSessionCannotAuthenticateAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("race@example.test", "OriginalPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var management = new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var protector = new InterleavedSecretProtector(() => management.ChangePasswordAsync(actor, new("OriginalPassword123!", "ChangedPassword123!")).GetAwaiter().GetResult());
		var login = new AccountService(new EfAccountStore(fixture.Database), protector, TimeProvider.System);

		var racedGrant = await login.LoginAsync(new("race@example.test", "OriginalPassword123!"));

		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(racedGrant.Token))).StatusCode);
		var current = await fixture.Service.LoginAsync(new("race@example.test", "ChangedPassword123!"));
		Assert.True((await fixture.Service.AuthenticateAsync(current.Token)).IsHuman);
	}

	[Fact]
	public async Task CreateTokenAsync_WhenInFlightActorPredatesPasswordChange_NewTokenCannotAuthenticateAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("race@example.test", "OriginalPassword123!", "test"));
		AuthenticatedActor staleActor = await fixture.Service.AuthenticateAsync(grant.Token);
		var management = new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		await management.ChangePasswordAsync(staleActor, new("OriginalPassword123!", "ChangedPassword123!"));
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);

		var racedGrant = await agents.CreateTokenAsync(staleActor, new("In-flight request"));

		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(racedGrant.Token))).StatusCode);
		var current = await fixture.Service.LoginAsync(new("race@example.test", "ChangedPassword123!"));
		var valid = await agents.CreateTokenAsync(await fixture.Service.AuthenticateAsync(current.Token), new("Current request"));
		Assert.False((await fixture.Service.AuthenticateAsync(valid.Token)).IsHuman);
	}

	private sealed class InterleavedSecretProtector(Action afterVerification) : ISecretProtector
	{
		private readonly SecretProtector inner = new();
		public string CreateToken(bool isAgent) => inner.CreateToken(isAgent);
		public string HashPassword(string password) => inner.HashPassword(password);
		public string HashToken(string token) => inner.HashToken(token);
		public bool VerifyPassword(string passwordHash, string password)
		{
			bool valid = inner.VerifyPassword(passwordHash, password);
			if (valid)
				afterVerification();
			return valid;
		}
	}
}
