namespace PlanForge.Infrastructure.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Contracts.Accounts;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Database;

public sealed class AccountServiceTests
{
	[Fact]
	public async Task RegisterAsync_WhenValid_PersistsNormalizedUserAndHashedHumanSessionAsync()
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		var options = new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlite(connection).Options;
		await using var database = new PlanForgeDbContext(options);
		await database.Database.EnsureCreatedAsync();
		var service = new AccountService(new EfAccountStore(database), new SecretProtector(), TimeProvider.System);

		AccessGrant result = await service.RegisterAsync(new RegisterRequest(" Test@Example.Invalid ", "Test-only-passphrase-739!", "test"));

		UserAccount user = Assert.Single(database.Users);
		AccessCredential credential = Assert.Single(database.AccessCredentials);
		Assert.Equal("TEST@EXAMPLE.INVALID", user.NormalizedEmail);
		Assert.Equal("test", result.User.DisplayName);
		Assert.NotEqual("Test-only-passphrase-739!", user.PasswordHash);
		Assert.NotEqual(result.Token, credential.TokenHash);
		Assert.Equal(64, credential.TokenHash.Length);
		Assert.Equal(CredentialKind.HumanSession, credential.Kind);
		Assert.Equal(user.Id, credential.OwnerUserId);
		Assert.Equal(TimeSpan.FromDays(7), credential.ExpiresAt - credential.CreatedAt);
	}
}
