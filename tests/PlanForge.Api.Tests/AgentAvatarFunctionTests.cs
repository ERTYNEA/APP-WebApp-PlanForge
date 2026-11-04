namespace PlanForge.Api.Tests;

using PlanForge.Api.Agents;
using PlanForge.Application.Agents;
using PlanForge.Contracts.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;
using System.Net;

public sealed class AgentAvatarFunctionTests
{
	[Fact]
	public async Task AvatarEndpoint_WhenInvoked_StoresReadsAndDeletesRasterWithOwnerAndHumanGuardsAsync()
	{
		await using var fixture = new AccountHttpFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		var actor = await fixture.Service.AuthenticateAsync(a.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(actor, new("Image agent"));
		var agentToken = await agents.CreateTokenAsync(actor, new("Agent token", agent.Id));
		var functions = new AgentAvatarFunctions(agents, fixture.Service, fixture.Runner);
		byte[] image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
		HttpTestRequest Upload(string token)
		{
			var request = new HttpTestRequest(method: "PUT", bytes: image);
			request.Headers.Add("Authorization", "Bearer " + token);
			request.Headers.Add("Content-Type", "image/png");
			return request;
		}
		Assert.Equal(HttpStatusCode.NotFound, (await functions.AvatarAsync(Upload(b.Token), agent.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, (await functions.AvatarAsync(Upload(agentToken.Token), agent.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await functions.AvatarAsync(Upload(a.Token), agent.Id)).StatusCode);
		var get = new HttpTestRequest(method: "GET");
		get.Headers.Add("Authorization", "Bearer " + a.Token);
		var response = await functions.AvatarAsync(get, agent.Id);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(image, ((MemoryStream)response.Body).ToArray());
		Assert.StartsWith("inline;", response.Headers.GetValues("Content-Disposition").Single());
		var otherGet = new HttpTestRequest(method: "GET");
		otherGet.Headers.Add("Authorization", "Bearer " + b.Token);
		Assert.Equal(HttpStatusCode.NotFound, (await functions.AvatarAsync(otherGet, agent.Id)).StatusCode);
		var delete = new HttpTestRequest(method: "DELETE");
		delete.Headers.Add("Authorization", "Bearer " + a.Token);
		Assert.Equal(HttpStatusCode.OK, (await functions.AvatarAsync(delete, agent.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await functions.AvatarAsync(get, agent.Id)).StatusCode);
	}
}
