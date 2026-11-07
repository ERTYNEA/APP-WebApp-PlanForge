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

		var refused = await Assert.ThrowsAsync<PlanForgeException>(() => agents.CreateTokenAsync(staleActor, new("In-flight request")));

		Assert.Equal(409, refused.StatusCode);
		Assert.DoesNotContain(await agents.ListTokensAsync(staleActor), token => token.Name == "In-flight request");
		var current = await fixture.Service.LoginAsync(new("race@example.test", "ChangedPassword123!"));
		var valid = await agents.CreateTokenAsync(await fixture.Service.AuthenticateAsync(current.Token), new("Current request"));
		Assert.False((await fixture.Service.AuthenticateAsync(valid.Token)).IsHuman);
	}

	[Fact]
	public async Task CreateTokenAsync_WhenAgentDeactivatesAndReactivatesDuringIssuance_PreviousActivationCannotAuthenticateAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("agent-race@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor owner = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(owner, new("Race agent"));
		var old = await agents.CreateTokenAsync(owner, new("Previously issued", agent.Id));
		var personal = await agents.CreateTokenAsync(owner, new("Personal"));
		var protector = new InterleavedTokenProtector(() =>
		{
			agents.UpdateAsync(owner, agent.Id, new("Race agent", IsActive: false)).GetAwaiter().GetResult();
			agents.UpdateAsync(owner, agent.Id, new("Race agent", IsActive: true)).GetAwaiter().GetResult();
		});
		var issuance = new AgentService(new EfAgentStore(fixture.Database), protector, TimeProvider.System);

		var refused = await Assert.ThrowsAsync<PlanForgeException>(() => issuance.CreateTokenAsync(owner, new("In-flight", agent.Id)));

		Assert.Equal(409, refused.StatusCode);
		Assert.DoesNotContain(await agents.ListTokensAsync(owner), token => token.Name == "In-flight");
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(protector.LastToken!))).StatusCode);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(old.Token))).StatusCode);
		Assert.False((await fixture.Service.AuthenticateAsync(personal.Token)).IsHuman);
		var fresh = await agents.CreateTokenAsync(owner, new("After reactivation", agent.Id));
		Assert.Equal(agent.Id, (await fixture.Service.AuthenticateAsync(fresh.Token)).AgentId);
	}

	[Fact]
	public async Task CreateTokenAsync_WhenAgentDeletedDuringIssuance_DoesNotPersistOrAuthenticateOrphanCredentialAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("delete-in-flight@example.test", "CorrectPassword123!", "test"));
		var owner = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(owner, new("In-flight deletion"));
		var protector = new InterleavedTokenProtector(() => agents.DeleteAsync(owner, agent.Id).GetAwaiter().GetResult());
		var issuance = new AgentService(new EfAgentStore(fixture.Database), protector, TimeProvider.System);

		var refusal = await Assert.ThrowsAsync<PlanForgeException>(() => issuance.CreateTokenAsync(owner, new("Racing issuance", agent.Id)));
		Assert.Equal(409, refusal.StatusCode);

		fixture.Database.ChangeTracker.Clear();
		Assert.NotNull(protector.LastToken);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(protector.LastToken))).StatusCode);
		Assert.Empty(await agents.ListAsync(owner));
		Assert.Empty(await agents.ListTokensAsync(owner));
		Assert.True((await fixture.Service.AuthenticateAsync(grant.Token)).IsHuman);
	}

	private sealed class InterleavedTokenProtector(Action beforeIssuance) : ISecretProtector
	{
		private readonly SecretProtector inner = new();
		public string? LastToken { get; private set; }
		public string CreateToken(bool isAgent) { beforeIssuance(); return LastToken = inner.CreateToken(isAgent); }
		public string HashPassword(string password) => inner.HashPassword(password);
		public string HashToken(string token) => inner.HashToken(token);
		public bool VerifyPassword(string passwordHash, string password) => inner.VerifyPassword(passwordHash, password);
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
