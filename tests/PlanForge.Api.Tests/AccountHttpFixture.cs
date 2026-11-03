namespace PlanForge.Api.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlanForge.Api.Accounts;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Database;

public sealed class AccountHttpFixture : IAsyncDisposable
{
	private readonly SqliteConnection connection = new("Data Source=:memory:");

	public AccountHttpFixture()
	{
		connection.Open();
		Database = new PlanForgeDbContext(new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlite(connection).Options);
		Database.Database.EnsureCreated();
		Service = new AccountService(new EfAccountStore(Database), new SecretProtector(), TimeProvider.System);
		Runner = new HttpEndpointRunner();
		Functions = new AccountFunctions(Service, Runner);
	}

	public PlanForgeDbContext Database { get; }
	public AccountFunctions Functions { get; }
	public HttpEndpointRunner Runner { get; }
	public AccountService Service { get; }

	public async ValueTask DisposeAsync()
	{
		await Database.DisposeAsync();
		await connection.DisposeAsync();
	}
}
