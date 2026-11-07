namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Application.Agents;
using PlanForge.Application.Common;
using PlanForge.Contracts.Agents;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;
using PlanForge.Infrastructure.Database;

public sealed class AccountRevocationRaceTests
{
	[Fact]
	public async Task PasswordChange_WhenTokenCommitsBeforeUserGate_FinalRevocationKeepsHistoryConsistentAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var session = await fixture.Service.RegisterAsync(new("account-final-scan@example.test", "OriginalPassword123!", "test"));
		var owner = await fixture.Service.AuthenticateAsync(session.Token);
		AgentTokenGrant? inserted = null;
		var interleavedStore = new BeforeSaveStore(new EfAccountStore(fixture.Database), async () =>
		{
			await using var issuerDatabase = new PlanForgeDbContext(new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlite(fixture.Database.Database.GetDbConnection()).Options);
			var issuer = new AgentService(new EfAgentStore(issuerDatabase), new SecretProtector(), TimeProvider.System);
			inserted = await issuer.CreateTokenAsync(owner, new("Late personal token"));
		});
		var management = new AccountManagementService(interleavedStore, new SecretProtector(), TimeProvider.System);

		await management.ChangePasswordAsync(owner, new("OriginalPassword123!", "ChangedPassword123!"));

		Assert.NotNull(inserted);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		Assert.NotNull(Assert.Single(await agents.ListTokensAsync(owner), token => token.Id == inserted.Credential.Id).RevokedAt);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(inserted.Token))).StatusCode);
		var fresh = await fixture.Service.LoginAsync(new("account-final-scan@example.test", "ChangedPassword123!"));
		var currentOwner = await fixture.Service.AuthenticateAsync(fresh.Token);
		var valid = await agents.CreateTokenAsync(currentOwner, new("Current-generation survivor"));
		Assert.Equal(1, await agents.CleanTokensAsync(currentOwner, new(true)));
		Assert.Equal(valid.Credential.Id, (await fixture.Service.AuthenticateAsync(valid.Token)).CredentialId);
	}

	// Controls timing only; all account/credential operations remain real relational calls.
	private sealed class BeforeSaveStore(IAccountStore inner, Func<Task> beforeSave) : IAccountStore
	{
		private bool invoked;
		public Task AddCredentialAsync(AccessCredential credential, CancellationToken cancellationToken) => inner.AddCredentialAsync(credential, cancellationToken);
		public Task AddUserAsync(UserAccount user, CancellationToken cancellationToken) => inner.AddUserAsync(user, cancellationToken);
		public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken) => inner.FindByEmailAsync(email, cancellationToken);
		public Task<AccessCredential?> FindCredentialAsync(string hash, CancellationToken cancellationToken) => inner.FindCredentialAsync(hash, cancellationToken);
		public Task<AccessCredential?> FindCredentialByIdAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) => inner.FindCredentialByIdAsync(id, ownerId, cancellationToken);
		public Task<UserAccount?> FindUserAsync(Guid id, CancellationToken cancellationToken) => inner.FindUserAsync(id, cancellationToken);
		public Task<UserAccount?> FindUserForUpdateAsync(Guid id, CancellationToken cancellationToken) => inner.FindUserForUpdateAsync(id, cancellationToken);
		public Task<string?> GetActiveAgentNameAsync(Guid ownerId, Guid agentId, long version, CancellationToken cancellationToken) => inner.GetActiveAgentNameAsync(ownerId, agentId, version, cancellationToken);
		public Task<IReadOnlyList<AccessCredential>> ListCredentialsAsync(Guid ownerId, CancellationToken cancellationToken) => inner.ListCredentialsAsync(ownerId, cancellationToken);
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
