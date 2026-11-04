namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Agents;
using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Agents;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Agents;
using PlanForge.Infrastructure.Accounts;

public sealed class AgentServiceTests
{
	[Fact]
	public async Task TokenLifecycle_WhenRenewedRevokedOrExpired_RejectsOldTokensAndPreventsAgentIssuanceAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var clock = new MutableTimeProvider();
		var service = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), clock);
		var accounts = new AccountService(new EfAccountStore(fixture.Database), new SecretProtector(), clock);
		AgentTokenGrant original = await service.CreateTokenAsync(actor, new CreateAgentTokenRequest("Original"));
		AuthenticatedActor agent = await accounts.AuthenticateAsync(original.Token);
		Assert.Equal(403, (await Assert.ThrowsAsync<PlanForgeException>(() => service.CreateTokenAsync(agent, new CreateAgentTokenRequest("Forbidden")))).StatusCode);

		AgentTokenGrant renewed = await service.RenewTokenAsync(actor, original.Credential.Id);
		Assert.NotEqual(original.Token, renewed.Token);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => accounts.AuthenticateAsync(original.Token))).StatusCode);
		Assert.False((await accounts.AuthenticateAsync(renewed.Token)).IsHuman);
		Assert.Equal(2, (await service.ListTokensAsync(actor)).Count);
		await service.RevokeTokenAsync(actor, renewed.Credential.Id);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => accounts.AuthenticateAsync(renewed.Token))).StatusCode);
		AgentTokenGrant expires = await service.CreateTokenAsync(actor, new CreateAgentTokenRequest("Expires"));
		clock.Now = expires.Credential.ExpiresAt;
		Assert.Contains("expired", (await Assert.ThrowsAsync<PlanForgeException>(() => accounts.AuthenticateAsync(expires.Token))).Message);
	}

	[Fact]
	public async Task UpdateAsync_WhenAgentDisabled_RevokesTokensAndKeepsHistoricalIdentityAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		AgentProfile agent = await service.CreateAsync(actor, new AgentRequest("Agent"));
		AgentTokenGrant token = await service.CreateTokenAsync(actor, new CreateAgentTokenRequest("Token", agent.Id));

		AgentProfile disabled = await service.UpdateAsync(actor, agent.Id, new AgentRequest("Inactive agent", IsActive: false));

		Assert.False(disabled.IsActive);
		Assert.Single(await service.ListAsync(actor));
		Assert.NotNull((await service.ListTokensAsync(actor)).Single().RevokedAt);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(token.Token))).StatusCode);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => service.CreateTokenAsync(actor, new CreateAgentTokenRequest("New", agent.Id)))).StatusCode);
		// Simulate a token created concurrently just before deactivation committed.
		fixture.Database.AccessCredentials.Single(credential => credential.Id == token.Credential.Id).RevokedAt = null;
		await fixture.Database.SaveChangesAsync();
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(token.Token))).StatusCode);
	}

	[Fact]
	public async Task AvatarAsync_WhenOwned_PreservesRasterBytesAndDeniesOtherOwnerOrAgentMutationAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(a.Token);
		AuthenticatedActor other = await fixture.Service.AuthenticateAsync(b.Token);
		var service = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		AgentProfile agent = await service.CreateAsync(actor, new("Avatar agent"));
		byte[] image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
		Assert.True((await service.SetAvatarAsync(actor, agent.Id, image, "image/png")).HasAvatar);
		Assert.True((await service.ListAsync(actor)).Single().HasAvatar);
		Assert.Equal(image, (await service.GetAvatarAsync(actor, agent.Id)).Content);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => service.GetAvatarAsync(other, agent.Id))).StatusCode);
		Assert.Equal(403, (await Assert.ThrowsAsync<PlanForgeException>(() => service.SetAvatarAsync(actor with { Kind = CredentialKind.AgentToken }, agent.Id, image, "image/png"))).StatusCode);
		Assert.False((await service.SetAvatarAsync(actor, agent.Id, null, null)).HasAvatar);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => service.GetAvatarAsync(actor, agent.Id))).StatusCode);
		Assert.Empty(fixture.Database.Resources);
	}

	private sealed class MutableTimeProvider : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
		public override DateTimeOffset GetUtcNow() => Now;
	}

	[Fact]
	public async Task CreateTokenAsync_WhenHuman_CreatesOwnerBoundHashedTokenExpiringInThirtyDaysAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		AgentProfile agent = await service.CreateAsync(actor, new AgentRequest("Integration agent", "Planning"));

		AgentTokenGrant token = await service.CreateTokenAsync(actor, new CreateAgentTokenRequest("Test token", agent.Id));

		Assert.StartsWith("pf_a_", token.Token);
		Assert.Equal(TimeSpan.FromDays(30), token.Credential.ExpiresAt - token.Credential.CreatedAt);
		AccessCredential stored = fixture.Database.AccessCredentials.Single(item => item.Id == token.Credential.Id);
		Assert.NotEqual(token.Token, stored.TokenHash);
		Assert.Equal(64, stored.TokenHash.Length);
		Assert.Equal(actor.UserId, stored.OwnerUserId);
		Assert.Equal(agent.Id, stored.AgentId);
		Assert.False((await fixture.Service.AuthenticateAsync(token.Token)).IsHuman);
	}
}
