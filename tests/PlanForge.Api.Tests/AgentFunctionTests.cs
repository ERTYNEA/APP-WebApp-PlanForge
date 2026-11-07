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

	[Fact]
	public async Task DeleteAsync_WhenConfirmedByHuman_PhysicallyRemovesAgentAvatarAndEveryLinkedTokenAsync()
	{
		await using var fixture = new AccountHttpFixture();
		AccessGrant grant = await fixture.Service.RegisterAsync(new("delete@example.test", "CorrectPassword123!", "test"));
		var service = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		AgentProfile agent = await service.CreateAsync(actor, new("Disposable agent"));
		AgentTokenGrant token = await service.CreateTokenAsync(actor, new("Linked", agent.Id));
		await service.RevokeTokenAsync(actor, token.Credential.Id);
		await service.CreateTokenAsync(actor, new("Active", agent.Id));
		AgentTokenGrant personal = await service.CreateTokenAsync(actor, new("Personal"));
		byte[] image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
		await service.SetAvatarAsync(actor, agent.Id, image, "image/png");
		var functions = new AgentFunctions(service, fixture.Service, fixture.Runner);

		Assert.Equal(HttpStatusCode.BadRequest, (await functions.AgentAsync(Request(grant.Token, null, "DELETE"), agent.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, (await functions.AgentAsync(Request(grant.Token, new { confirmed = false }, "DELETE"), agent.Id)).StatusCode);
		var response = await functions.AgentAsync(Request(grant.Token, new { confirmed = true }, "DELETE"), agent.Id);

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		fixture.Database.ChangeTracker.Clear();
		Assert.DoesNotContain(fixture.Database.Agents, entry => entry.Id == agent.Id);
		Assert.DoesNotContain(fixture.Database.AccessCredentials, entry => entry.AgentId == agent.Id);
		Assert.Empty(await service.ListAsync(actor));
		Assert.False((await fixture.Service.AuthenticateAsync(personal.Token)).IsHuman);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.AgentsAsync(Request(token.Token, null, "GET"))).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await functions.AgentAsync(Request(grant.Token, null, "DELETE"), agent.Id)).StatusCode);
	}

	[Fact]
	public async Task DeleteAsync_WhenForeignOrNonHuman_DeniesWithoutMutatingAnyAgentAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var first = await fixture.Service.RegisterAsync(new("owner-boundary@example.test", "CorrectPassword123!", "first"));
		var second = await fixture.Service.RegisterAsync(new("foreign-boundary@example.test", "CorrectPassword123!", "second"));
		var service = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var owner = await fixture.Service.AuthenticateAsync(first.Token);
		var agent = await service.CreateAsync(owner, new("Boundary agent"));
		var linked = await service.CreateTokenAsync(owner, new("Linked", agent.Id));
		var personal = await service.CreateTokenAsync(owner, new("Personal"));
		var functions = new AgentFunctions(service, fixture.Service, fixture.Runner);
		Assert.Equal(HttpStatusCode.NotFound, (await functions.AgentAsync(Request(second.Token, new DeleteAgentRequest(true), "DELETE"), agent.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, (await functions.AgentAsync(Request(linked.Token, new DeleteAgentRequest(true), "DELETE"), agent.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, (await functions.AgentAsync(Request(personal.Token, new DeleteAgentRequest(true), "DELETE"), agent.Id)).StatusCode);
		Assert.Single(await service.ListAsync(owner));
		Assert.Equal(agent.Id, (await fixture.Service.AuthenticateAsync(linked.Token)).AgentId);
		Assert.Equal(2, (await service.ListTokensAsync(owner)).Count);
	}

	[Fact]
	public async Task CleanTokensAsync_WhenConfirmed_DeletesOnlyOwnersRevokedAndExpiredAgentTokensAsync()
	{
		await using var fixture = new AccountHttpFixture();
		AccessGrant grant = await fixture.Service.RegisterAsync(new("clean@example.test", "CorrectPassword123!", "test"));
		AccessGrant otherGrant = await fixture.Service.RegisterAsync(new("other@example.test", "CorrectPassword123!", "other"));
		var service = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var owner = await fixture.Service.AuthenticateAsync(grant.Token);
		var other = await fixture.Service.AuthenticateAsync(otherGrant.Token);
		AgentTokenGrant revoked = await service.CreateTokenAsync(owner, new("Revoked"));
		await service.RevokeTokenAsync(owner, revoked.Credential.Id);
		AgentTokenGrant expired = await service.CreateTokenAsync(owner, new("Expired"));
		fixture.Database.AccessCredentials.Single(entry => entry.Id == expired.Credential.Id).ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
		await fixture.Database.SaveChangesAsync();
		AgentTokenGrant active = await service.CreateTokenAsync(owner, new("Valid"));
		AgentTokenGrant foreign = await service.CreateTokenAsync(other, new("Foreign revoked"));
		await service.RevokeTokenAsync(other, foreign.Credential.Id);
		var functions = new AgentFunctions(service, fixture.Service, fixture.Runner);

		Assert.Equal(HttpStatusCode.BadRequest, (await functions.CleanTokensAsync(Request(grant.Token, new CleanAgentTokensRequest(false)))).StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, (await functions.CleanTokensAsync(Request(active.Token, new CleanAgentTokensRequest(true)))).StatusCode);
		var response = await functions.CleanTokensAsync(Request(grant.Token, new CleanAgentTokensRequest(true)));

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		response.Body.Position = 0;
		using JsonDocument result = await JsonDocument.ParseAsync(response.Body);
		Assert.Equal(2, result.RootElement.GetProperty("deletedCount").GetInt32());
		fixture.Database.ChangeTracker.Clear();
		Assert.DoesNotContain(fixture.Database.AccessCredentials, entry => entry.Id == revoked.Credential.Id || entry.Id == expired.Credential.Id);
		Assert.Contains(fixture.Database.AccessCredentials, entry => entry.Id == foreign.Credential.Id);
		Assert.Equal(active.Credential.Id, (await fixture.Service.AuthenticateAsync(active.Token)).CredentialId);
		Assert.True((await fixture.Service.AuthenticateAsync(grant.Token)).IsHuman);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.AgentsAsync(Request(expired.Token, null, "GET"))).StatusCode);
		var repeated = await functions.CleanTokensAsync(Request(grant.Token, new CleanAgentTokensRequest(true)));
		repeated.Body.Position = 0;
		using JsonDocument second = await JsonDocument.ParseAsync(repeated.Body);
		Assert.Equal(0, second.RootElement.GetProperty("deletedCount").GetInt32());
	}

	private static HttpTestRequest Request(string token, object? body, string method = "POST")
	{
		var request = new HttpTestRequest(body is null ? "" : JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), method);
		request.Headers.Add("Authorization", "Bearer " + token);
		return request;
	}
}
