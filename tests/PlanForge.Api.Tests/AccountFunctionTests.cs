namespace PlanForge.Api.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlanForge.Api.Accounts;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Database;
using System.Net;

public sealed class AccountFunctionTests
{
	[Fact]
	public async Task RegisterAsync_WhenValidHttpRequest_ReturnsCreatedAndPersistsAsync()
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		await using var database = new PlanForgeDbContext(new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlite(connection).Options);
		await database.Database.EnsureCreatedAsync();
		var functions = new AccountFunctions(new AccountService(new EfAccountStore(database), new SecretProtector(), TimeProvider.System), new HttpEndpointRunner());
		var request = new HttpTestRequest("""{"email":"test@example.invalid","password":"Test-only-password-739!","displayName":"test"}""");

		var response = await functions.RegisterAsync(request);

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		Assert.Single(database.Users);
		Assert.Single(database.AccessCredentials);
		Assert.Equal("no-store", response.Headers.GetValues("Cache-Control").Single());
	}
}
