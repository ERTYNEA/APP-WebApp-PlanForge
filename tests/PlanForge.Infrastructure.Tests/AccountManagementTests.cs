namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Agents;
using PlanForge.Application.Common;
using PlanForge.Application.Library;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Agents;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;
using PlanForge.Infrastructure.Library;

public sealed class AccountManagementTests
{
	[Fact]
	public async Task ProfileAsync_WhenNameAndAvatarUpdated_PreservesIdentityAndExcludesAvatarFromOrphanCleanupAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var profile = await service.UpdateAsync(actor, new("Changed name"));
		Assert.Equal(actor.UserId, profile.Id);
		Assert.Equal(grant.User.Email, profile.Email);
		Assert.Equal("Changed name", (await fixture.Service.GetProfileAsync(actor)).DisplayName);
		byte[] image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
		profile = await service.SetAvatarAsync(actor, image, "image/png");
		Assert.True(profile.HasAvatar);
		Assert.True((await fixture.Service.GetProfileAsync(actor)).HasAvatar);
		Assert.Equal(image, (await service.GetAvatarAsync(actor)).Content);
		var library = new LibraryService(new EfLibraryStore(fixture.Database), new ResourceLimits(), TimeProvider.System);
		var resource = await library.UploadAsync(actor, new("avatar-copy.png", "Library copy", "image/png"), image);
		Assert.Equal(1, await library.CleanOrphansAsync(actor, new(true, [resource.Id])));
		Assert.Equal(image, (await service.GetAvatarAsync(actor)).Content);
		Assert.Equal(403, (await Assert.ThrowsAsync<PlanForgeException>(() => service.UpdateAsync(actor with { Kind = CredentialKind.AgentToken }, new("Forbidden")))).StatusCode);
		Assert.False((await service.SetAvatarAsync(actor, null, null)).HasAvatar);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => service.GetAvatarAsync(actor))).StatusCode);
	}

	[Fact]
	public async Task ChangePasswordAsync_WhenVerified_RevokesOnlyOwnersCredentialsAndRequiresNewPasswordAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(a.Token);
		var agents = new AgentService(new EfAgentStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		var token = await agents.CreateTokenAsync(actor, new CreateAgentTokenRequest("Token"));
		var service = new AccountManagementService(new EfAccountStore(fixture.Database), new SecretProtector(), TimeProvider.System);
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => service.ChangePasswordAsync(actor, new("Incorrect", "NewPassword123!")))).StatusCode);
		Assert.True((await fixture.Service.AuthenticateAsync(a.Token)).IsHuman);
		await service.ChangePasswordAsync(actor, new("CorrectPassword123!", "NewPassword123!"));
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(a.Token))).StatusCode);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.AuthenticateAsync(token.Token))).StatusCode);
		Assert.True((await fixture.Service.AuthenticateAsync(b.Token)).IsHuman);
		Assert.Equal(401, (await Assert.ThrowsAsync<PlanForgeException>(() => fixture.Service.LoginAsync(new("a@example.test", "CorrectPassword123!")))).StatusCode);
		Assert.NotNull(await fixture.Service.LoginAsync(new("a@example.test", "NewPassword123!")));
		Assert.NotEqual("NewPassword123!", fixture.Database.Users.Single(user => user.Id == actor.UserId).PasswordHash);
	}
}
