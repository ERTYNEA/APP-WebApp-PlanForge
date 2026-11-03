namespace PlanForge.Api.Tests;

using PlanForge.Api.Health;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

public sealed class HealthFunctionTests
{
	[Fact]
	public async Task ReadyAsync_WhenDatabaseUnavailable_ReturnsServiceUnavailableAsync()
	{
		await using var fixture = new AccountHttpFixture();
		await fixture.Database.Database.GetDbConnection().CloseAsync();
		var functions = new HealthFunctions(fixture.Database, fixture.Runner);

		var response = await functions.ReadyAsync(new HttpTestRequest(method: "GET"));

		Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
	}

	[Fact]
	public async Task HealthAsync_WhenRunning_ReturnsCentralSemanticVersionAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var functions = new HealthFunctions(fixture.Database, fixture.Runner);

		var response = await functions.HealthAsync(new HttpTestRequest(method: "GET"));
		response.Body.Position = 0;
		using JsonDocument body = await JsonDocument.ParseAsync(response.Body);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.StartsWith("0.1.0-alpha.3", body.RootElement.GetProperty("version").GetString());
	}

	[Fact]
	public async Task ReadyAsync_WhenSchemaAccessible_ReturnsReadyAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var functions = new HealthFunctions(fixture.Database, fixture.Runner);

		var response = await functions.ReadyAsync(new HttpTestRequest(method: "GET"));
		response.Body.Position = 0;
		using JsonDocument body = await JsonDocument.ParseAsync(response.Body);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("ready", body.RootElement.GetProperty("status").GetString());
	}
}
