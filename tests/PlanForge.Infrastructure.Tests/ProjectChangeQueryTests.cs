namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;
using PlanForge.Application.Planning;
using PlanForge.Infrastructure.Planning;

public sealed class ProjectChangeQueryTests
{
	[Fact]
	public async Task ChangesAsync_WhenCursorNegative_RejectsInvalidCursorAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var project = await service.CreateAsync(actor, new("Project"));
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => service.GetChangesAsync(actor, project.Id, -1))).StatusCode);
	}

	[Fact]
	public async Task ChangesAsync_WhenQueried_ReturnsActualActorCursorAndOwnerFilteredEventsAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var project = await service.CreateAsync(actor, new("Observed project"));
		var empty = await service.GetChangesAsync(actor, project.Id);
		Assert.Equal(0, empty.LatestChangeId);
		Assert.Empty(empty.Changes);
		project = await service.SaveAsync(actor, project.Id, project with { Description = "Changed" });
		var page = await service.GetChangesAsync(actor, project.Id);
		Assert.Equal(project.Revision, page.Revision);
		var change = Assert.Single(page.Changes);
		Assert.Equal("Human", change.ActorKind);
		Assert.Equal(actor.UserId, change.ActorId);
		Assert.Equal(fixture.Database.Users.Single().DisplayName, change.ActorName);
		Assert.Equal("Project updated", change.Action);
		Assert.Empty((await service.GetChangesAsync(actor, project.Id, page.LatestChangeId)).Changes);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => service.GetChangesAsync(actor with { UserId = Guid.NewGuid() }, project.Id))).StatusCode);
		Assert.True((await service.GetChangesAsync(actor, project.Id, long.MaxValue)).ResetRequired);
	}
}
