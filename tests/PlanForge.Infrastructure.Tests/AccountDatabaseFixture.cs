namespace PlanForge.Infrastructure.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Database;

public sealed class AccountDatabaseFixture : IAsyncDisposable
{
	private readonly SqliteConnection connection = new("Data Source=:memory:");

	public AccountDatabaseFixture()
	{
		connection.Open();
		Database = new PlanForgeDbContext(new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlite(connection).Options);
		Database.Database.EnsureCreated();
		Service = new AccountService(new EfAccountStore(Database), new SecretProtector(), TimeProvider.System);
	}

	public PlanForgeDbContext Database { get; }
	public AccountService Service { get; }

	public async ValueTask DisposeAsync()
	{
		await Database.DisposeAsync();
		await connection.DisposeAsync();
	}
}
