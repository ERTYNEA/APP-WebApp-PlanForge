namespace PlanForge.Api.Tests;

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using PlanForge.Api.Accounts;
using PlanForge.Api.Agents;
using PlanForge.Application.Accounts;
using PlanForge.Application.Agents;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;
using System.Net;

public sealed class AvatarSecurityHttpTests
{
	[Theory]
	[InlineData("GET")]
	[InlineData("PUT")]
	[InlineData("DELETE")]
	public async Task EveryAgentAvatarVerb_EnforcesAuthenticationAndOwnerBeforeMutationAsync(string method)
	{
		await using var fixture = new AccountHttpFixture();
		var a = await fixture.Service.RegisterAsync(new("owner@example.test", "CorrectPassword123!", "QA"));
		var b = await fixture.Service.RegisterAsync(new("other@example.test", "CorrectPassword123!", "QA isolation"));
		var actor = await fixture.Service.AuthenticateAsync(a.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(actor, new("Owned fixture"));
		var functions = new AgentAvatarFunctions(agents, fixture.Service, fixture.Runner, fixture.Avatars);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.AvatarAsync(Request(null, method), agent.Id)).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await functions.AvatarAsync(Request(b.Token, method), agent.Id)).StatusCode);
		Assert.Empty(await fixture.Database.AvatarOptimizationJobs.ToListAsync());
		Assert.Equal(0, fixture.Signals.Count);
		Assert.Equal(2, await fixture.Database.Users.CountAsync());
		Assert.Equal(1, await fixture.Database.Agents.CountAsync());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CorruptOrSpoofedInput_PreservesAcceptedPhotoAndDoesNotSendSuccessWakeAsync(bool agent)
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new("secure-avatar@example.test", "CorrectPassword123!", "QA"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		Guid? agentId = agent ? (await agents.CreateAsync(actor, new("Owned fixture"))).Id : null;
		Task<HttpResponseData> InvokeAsync(HttpTestRequest request) => agentId.HasValue
			? new AgentAvatarFunctions(agents, fixture.Service, fixture.Runner, fixture.Avatars).AvatarAsync(request, agentId.Value)
			: new ProfileFunctions(fixture.Service, new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System), fixture.Runner, fixture.Avatars).AvatarAsync(request);
		byte[] original = AvatarHttpImage.Create();
		Assert.Equal(HttpStatusCode.OK, (await InvokeAsync(Request(grant.Token, "PUT", original))).StatusCode);
		var invalid = Request(grant.Token, "PUT", [137, 80, 78, 71, 13, 10, 26, 10]);
		Assert.Equal(HttpStatusCode.BadRequest, (await InvokeAsync(invalid)).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, (await InvokeAsync(Request(grant.Token, "PUT", original, "image/jpeg"))).StatusCode);
		Assert.Equal(1, fixture.Signals.Count);
		fixture.Database.ChangeTracker.Clear();
		Assert.Equal(original, agentId.HasValue ? (await fixture.Database.Agents.SingleAsync()).AvatarBytes : (await fixture.Database.Users.SingleAsync()).AvatarBytes);
		Assert.Single(await fixture.Database.AvatarOptimizationJobs.ToListAsync());
		Assert.Single(await fixture.Database.AvatarAdmissions.ToListAsync());
	}

	[Theory]
	[InlineData("GET")]
	[InlineData("PUT")]
	[InlineData("DELETE")]
	public async Task HumanAvatarEndpoint_RejectsAnonymousAndAgentSessionsForEveryVerbAsync(string method)
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new("human-only@example.test", "CorrectPassword123!", "QA"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var agent = await agents.CreateAsync(actor, new("Owned fixture"));
		var token = await agents.CreateTokenAsync(actor, new("Isolated credential", agent.Id));
		var functions = new ProfileFunctions(fixture.Service, new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System), fixture.Runner, fixture.Avatars);
		Assert.Equal(HttpStatusCode.Unauthorized, (await functions.AvatarAsync(Request(null, method))).StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, (await functions.AvatarAsync(Request(token.Token, method))).StatusCode);
		Assert.Equal(0, fixture.Signals.Count);
		Assert.Empty(await fixture.Database.AvatarOptimizationJobs.ToListAsync());
	}

	private static HttpTestRequest Request(string? token, string method, byte[]? bytes = null, string type = "image/png")
	{
		var request = new HttpTestRequest(method: method, bytes: bytes ?? AvatarHttpImage.Create());
		if (token is not null)
			request.Headers.Add("Authorization", "Bearer " + token);
		request.Headers.Add("Content-Type", type);
		return request;
	}
}
