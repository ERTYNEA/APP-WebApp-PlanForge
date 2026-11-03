namespace PlanForge.Api.Tests;

using PlanForge.Api.Planning;
using PlanForge.Application.Planning;
using PlanForge.Infrastructure.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using System.Net;
using System.Text.Json;

public sealed class ProjectFunctionTests
{
	[Fact]
	public async Task CollectionAsync_WhenAuthenticated_CreatesAndReadsActualProjectAsync()
	{
		await using var fixture = new AccountHttpFixture();
		AccessGrant grant = await fixture.Service.RegisterAsync(new RegisterRequest("test@example.invalid", "Test-only-password-739!", "test"));
		var functions = new ProjectFunctions(new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System), fixture.Service, fixture.Runner);
		var create = new HttpTestRequest("""{"name":"HTTP project","description":"Purpose"}""");
		create.Headers.Add("Authorization", "Bearer " + grant.Token);
		var response = await functions.CollectionAsync(create);
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		response.Body.Position = 0;
		ProjectSnapshot? snapshot = await JsonSerializer.DeserializeAsync<ProjectSnapshot>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
		Assert.NotNull(snapshot);
		Assert.Equal("HTTP project", snapshot.Name);
		var get = new HttpTestRequest(method: "GET");
		get.Headers.Add("Authorization", "Bearer " + grant.Token);
		Assert.Equal(HttpStatusCode.OK, (await functions.DetailAsync(get, snapshot.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.CollectionAsync(new HttpTestRequest(method: "GET"))).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.DetailAsync(new HttpTestRequest(method: "GET"), snapshot.Id)).StatusCode);
	}
}
