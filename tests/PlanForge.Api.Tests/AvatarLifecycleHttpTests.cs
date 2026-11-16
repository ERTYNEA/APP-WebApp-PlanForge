namespace PlanForge.Api.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Api.Accounts;
using PlanForge.Api.Agents;
using PlanForge.Application.Accounts;
using PlanForge.Application.Agents;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;
using SkiaSharp;
using System.Net;

public sealed class AvatarLifecycleHttpTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Upload_PersistsVisibleOriginalAndDurableJobThroughRealHandlerAsync(bool agent)
	{
		await using var fixture = new AccountHttpFixture();
		var grant = await fixture.Service.RegisterAsync(new("avatar-http@example.test", "CorrectPassword123!", "QA"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		Guid? agentId = agent ? (await agents.CreateAsync(actor, new("Avatar QA"))).Id : null;
		using var bitmap = new SKBitmap(20, 10);
		bitmap.Erase(SKColors.CornflowerBlue);
		using var raster = SKImage.FromBitmap(bitmap);
		using var encoded = raster.Encode(SKEncodedImageFormat.Png, 100);
		byte[] image = encoded.ToArray();
		var request = new HttpTestRequest(method: "PUT", bytes: image);
		request.Headers.Add("Authorization", "Bearer " + grant.Token);
		request.Headers.Add("Content-Type", "image/png");
		var response = agentId.HasValue
			? await new AgentAvatarFunctions(agents, fixture.Service, fixture.Runner, fixture.Avatars).AvatarAsync(request, agentId.Value)
			: await new ProfileFunctions(fixture.Service, new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System), fixture.Runner, fixture.Avatars).AvatarAsync(request);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		fixture.Database.ChangeTracker.Clear();
		var job = Assert.Single(await fixture.Database.AvatarOptimizationJobs.ToListAsync());
		Assert.Equal(actor.UserId, job.OwnerUserId);
		Assert.Equal(agentId, job.AgentId);
		Assert.Equal(PlanForge.Domain.Accounts.AvatarJobStatus.Pending, job.Status);
		Assert.Equal(image, agentId.HasValue ? (await fixture.Database.Agents.SingleAsync()).AvatarBytes : (await fixture.Database.Users.SingleAsync()).AvatarBytes);
		response.Body.Position = 0;
		using var profile = await System.Text.Json.JsonDocument.ParseAsync(response.Body);
		Assert.Equal(job.Generation, profile.RootElement.GetProperty("avatarVersion").GetGuid());
		var get = new HttpTestRequest(method: "GET");
		get.Headers.Add("Authorization", "Bearer " + grant.Token);
		var download = agentId.HasValue
			? await new AgentAvatarFunctions(agents, fixture.Service, fixture.Runner, fixture.Avatars).AvatarAsync(get, agentId.Value)
			: await new ProfileFunctions(fixture.Service, new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System), fixture.Runner, fixture.Avatars).AvatarAsync(get);
		Assert.Equal(HttpStatusCode.OK, download.StatusCode);
		Assert.True(download.Headers.TryGetValues("ETag", out var etags));
		Assert.Equal("\"" + job.Generation.ToString("N") + "\"", Assert.Single(etags));
	}
}
