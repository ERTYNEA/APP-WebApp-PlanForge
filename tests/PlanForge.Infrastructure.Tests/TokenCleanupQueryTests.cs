namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PlanForge.Application.Agents;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;
using PlanForge.Infrastructure.Database;
using System.Data.Common;

public sealed class TokenCleanupQueryTests
{
	[Fact]
	public async Task Cleanup_WhenExecuted_RechecksEligibilityInDatabaseWithoutReadingAllMetadataAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new("bounded-clean@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var expired = await agents.CreateTokenAsync(actor, new("Expired"));
		fixture.Database.AccessCredentials.Single(token => token.Id == expired.Credential.Id).ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
		await fixture.Database.SaveChangesAsync();
		var valid = await agents.CreateTokenAsync(actor, new("Survivor"));
		var observer = new ReadCounter();
		await using var observed = new PlanForgeDbContext(new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlite(fixture.Database.Database.GetDbConnection()).AddInterceptors(observer).Options);

		int deleted = await new EfAgentStore(observed).CleanTokensAsync(actor.UserId, DateTimeOffset.UtcNow, default);

		Assert.Equal(1, deleted);
		Assert.Equal(0, observer.Reads);
		Assert.Equal(valid.Credential.Id, (await fixture.Service.AuthenticateAsync(valid.Token)).CredentialId);
	}

	private sealed class ReadCounter : DbCommandInterceptor
	{
		public int Reads { get; private set; }
		public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
		{
			Reads++;
			return ValueTask.FromResult(result);
		}
	}
}
