namespace PlanForge.Api.Tests;

using PlanForge.Api.Agents;
using PlanForge.Application.Agents;
using PlanForge.Infrastructure.Agents;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Agents;
using System.Net;
using System.Text.Json;

public sealed class AgentFunctionTests
{
	[Fact]
	public async Task TokensAsync_WhenHuman_OnlyCreationAndRenewalRevealTokenAndRevocationIsImmediateAsync()
	{
		await using var fixture = new AccountHttpFixture();
		AccessGrant grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var functions = new AgentFunctions(new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System), fixture.Service, fixture.Runner);
		var created = await functions.AgentsAsync(Request(grant.Token, new AgentRequest("Agent")));
		Assert.Equal(HttpStatusCode.Created, created.StatusCode);
		created.Body.Position = 0;
		AgentProfile agent = (await JsonSerializer.DeserializeAsync<AgentProfile>(created.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
		var issued = await functions.TokensAsync(Request(grant.Token, new CreateAgentTokenRequest("Token", agent.Id)));
		Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
		issued.Body.Position = 0;
		AgentTokenGrant token = (await JsonSerializer.DeserializeAsync<AgentTokenGrant>(issued.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
		var listed = await functions.TokensAsync(Request(grant.Token, null, "GET"));
		listed.Body.Position = 0;
		Assert.DoesNotContain(token.Token, await new StreamReader(listed.Body).ReadToEndAsync());
		Assert.Equal(HttpStatusCode.Forbidden, (await functions.TokensAsync(Request(token.Token, new CreateAgentTokenRequest("Forbidden")))).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await functions.RenewAsync(Request(grant.Token, null), token.Credential.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.NoContent, (await functions.RevokeAsync(Request(grant.Token, null, "DELETE"), token.Credential.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.AgentsAsync(Request(token.Token, null, "GET"))).StatusCode);
	}

	private static HttpTestRequest Request(string token, object? body, string method = "POST")
	{
		var request = new HttpTestRequest(body is null ? "" : JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), method);
		request.Headers.Add("Authorization", "Bearer " + token);
		return request;
	}
}
