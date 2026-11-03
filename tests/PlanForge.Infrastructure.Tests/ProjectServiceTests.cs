namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;

public sealed class ProjectServiceTests
{
	[Fact]
	public async Task ReadAsync_WhenOtherOwner_RejectsKnownGuidAndFiltersListAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		AuthenticatedActor actorA = await fixture.Service.AuthenticateAsync(a.Token);
		AuthenticatedActor actorB = await fixture.Service.AuthenticateAsync(b.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await service.CreateAsync(actorA, new CreateProjectRequest("Duplicate name"));
		await service.CreateAsync(actorA, new CreateProjectRequest("Duplicate name"));

		Assert.Equal(project.Id, (await service.GetAsync(actorA, project.Id)).Id);
		Assert.Equal(2, (await service.ListAsync(actorA)).Count);
		Assert.Empty(await service.ListAsync(actorB));
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => service.GetAsync(actorB, project.Id))).StatusCode);
	}

	[Fact]
	public async Task CreateAsync_WhenValid_PersistsGuidOwnerAndInitialRevisionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);

		ProjectSnapshot project = await service.CreateAsync(actor, new CreateProjectRequest("Same name", "User description"));

		Assert.NotEqual(Guid.Empty, project.Id);
		Assert.Equal(1, project.Revision);
		Assert.Equal(actor.UserId, (await fixture.Database.Projects.FindAsync(project.Id))!.OwnerUserId);
		Assert.Empty(project.Items);
		Assert.Equal(project.UpdatedAt, (await service.GetAsync(actor, project.Id)).UpdatedAt);
	}
}
