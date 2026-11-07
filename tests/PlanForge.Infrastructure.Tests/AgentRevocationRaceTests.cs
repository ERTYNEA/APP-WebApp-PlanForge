namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Agents;
using PlanForge.Application.Common;
using PlanForge.Contracts.Agents;
using PlanForge.Domain.Accounts;
using PlanForge.Domain.Agents;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;
using PlanForge.Infrastructure.Database;

public sealed class AgentRevocationRaceTests
{
	[Fact]
	public async Task DeactivateAsync_WhenTokenCommitsBeforeGenerationGate_FinalRevocationIncludesItAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var session = await fixture.Service.RegisterAsync(new("final-scan@example.test", "CorrectPassword123!", "test"));
		var owner = await fixture.Service.AuthenticateAsync(session.Token);
		var originalStore = new EfAgentStore(fixture.Database);
		var agents = new AgentService(originalStore, new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(owner, new("Deactivation gap"));
		var personal = await agents.CreateTokenAsync(owner, new("Personal survivor"));
		AgentTokenGrant? inserted = null;
		var interleavedStore = new BeforeSaveStore(originalStore, async () =>
		{
			await using var issuerDatabase = new PlanForgeDbContext(new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlite(fixture.Database.Database.GetDbConnection()).Options);
			var issuer = new AgentService(new EfAgentStore(issuerDatabase), new SecretProtector(), TimeProvider.System);
			inserted = await issuer.CreateTokenAsync(owner, new("Inserted after revocation scan", agent.Id));
		});
		var deactivation = new AgentService(interleavedStore, new SecretProtector(), TimeProvider.System);

		await deactivation.UpdateAsync(owner, agent.Id, new("Deactivation gap", IsActive: false));

		Assert.NotNull(inserted);
		var metadata = Assert.Single(await agents.ListTokensAsync(owner), token => token.Id == inserted.Credential.Id);
		Assert.NotNull(metadata.RevokedAt);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(inserted.Token))).StatusCode);
		await agents.UpdateAsync(owner, agent.Id, new("Deactivation gap", IsActive: true));
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(inserted.Token))).StatusCode);
		Assert.Equal(1, await agents.CleanTokensAsync(owner, new(true)));
		Assert.Equal(personal.Credential.Id, (await fixture.Service.AuthenticateAsync(personal.Token)).CredentialId);
	}

	// Injects only request timing; every read/write still uses real relational EfAgentStore.
	private sealed class BeforeSaveStore(IAgentStore inner, Func<Task> beforeSave) : IAgentStore
	{
		private bool invoked;
		public void Add(Agent agent) => inner.Add(agent);
		public void Add(AccessCredential credential) => inner.Add(credential);
		public Task<int> CleanTokensAsync(Guid ownerId, DateTimeOffset now, CancellationToken cancellationToken) => inner.CleanTokensAsync(ownerId, now, cancellationToken);
		public Task DeleteAsync(Guid ownerId, Guid agentId, CancellationToken cancellationToken) => inner.DeleteAsync(ownerId, agentId, cancellationToken);
		public Task<Agent?> GetAsync(Guid ownerId, Guid agentId, CancellationToken cancellationToken) => inner.GetAsync(ownerId, agentId, cancellationToken);
		public Task<AccessCredential?> GetTokenAsync(Guid ownerId, Guid tokenId, CancellationToken cancellationToken) => inner.GetTokenAsync(ownerId, tokenId, cancellationToken);
		public Task<IReadOnlyList<Agent>> ListAsync(Guid ownerId, CancellationToken cancellationToken) => inner.ListAsync(ownerId, cancellationToken);
		public Task<IReadOnlyList<AccessCredential>> ListTokensAsync(Guid ownerId, CancellationToken cancellationToken) => inner.ListTokensAsync(ownerId, cancellationToken);
		public async Task SaveAsync(CancellationToken cancellationToken)
		{
			if (!invoked)
			{
				invoked = true;
				await beforeSave();
			}
			await inner.SaveAsync(cancellationToken);
		}
	}
}
