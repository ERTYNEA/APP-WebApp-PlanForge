namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;

public sealed class PlanRevisionTests
{
	[Fact]
	public async Task SaveAsync_WhenRecordsOmitted_RequiresExplicitReplaceAndDeletesOnlyInsideTargetAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await service.CreateAsync(actor, new CreateProjectRequest("Plan"));
		Guid phase = Guid.NewGuid();
		ProjectSnapshot saved = await service.SaveAsync(actor, project.Id, project with { Items = [new(phase, "Phase", null, 0, "Phase"), new(Guid.NewGuid(), "Subphase", phase, 0, "Subphase")] });

		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => service.SaveAsync(actor, saved.Id, saved with { Items = [] }))).StatusCode);
		Assert.Equal(2, (await service.GetAsync(actor, saved.Id)).Items.Count);
		ProjectSnapshot replaced = await service.SaveAsync(actor, saved.Id, saved with { Items = [] }, true);
		Assert.Equal(3, replaced.Revision);
		Assert.Empty((await service.GetAsync(actor, saved.Id)).Items);
		Assert.Equal(409, (await Assert.ThrowsAsync<PlanForgeException>(() => service.SaveAsync(actor, saved.Id, saved))).StatusCode);
	}

	[Fact]
	public async Task SaveAsync_WhenForeignRecordIdCollides_RollsBackRevisionAndAllRowsAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot first = await service.CreateAsync(actor, new CreateProjectRequest("First"));
		ProjectSnapshot second = await service.CreateAsync(actor, new CreateProjectRequest("Second"));
		Guid phase = Guid.NewGuid();
		first = await service.SaveAsync(actor, first.Id, first with { Items = [new(phase, "Phase", null, 0, "Original"), new(Guid.NewGuid(), "Subphase", phase, 0, "Subphase")] });
		ProjectSnapshot collision = second with { Items = [new(phase, "Phase", null, 0, "Collision"), new(Guid.NewGuid(), "Subphase", phase, 0, "Subphase")] };
		Assert.Equal(409, (await Assert.ThrowsAsync<PlanForgeException>(() => service.SaveAsync(actor, second.Id, collision))).StatusCode);
		Assert.Equal(1, (await service.GetAsync(actor, second.Id)).Revision);
		Assert.Empty((await service.GetAsync(actor, second.Id)).Items);
		Assert.Equal(second.EffectiveStatusChangedAt, (await service.GetAsync(actor, second.Id)).EffectiveStatusChangedAt);
		Assert.Equal(first.EffectiveStatusChangedAt, (await service.GetAsync(actor, first.Id)).EffectiveStatusChangedAt);
		ProjectSnapshot preserved = await service.GetAsync(actor, first.Id);
		Assert.All(first.Items, item => Assert.Equal(item.EffectiveStatusChangedAt, preserved.Items.Single(saved => saved.Id == item.Id).EffectiveStatusChangedAt));
		Assert.Equal("Original", (await service.GetAsync(actor, first.Id)).Items.Single(item => item.Id == phase).Title);
		Assert.Single(fixture.Database.ProjectChanges);
	}

	[Fact]
	public async Task DeleteAsync_WhenUnconfirmedOrOtherOwner_DeniesAndPreservesDataAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var a = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var b = await fixture.Service.RegisterAsync(new RegisterRequest("b@example.test", "CorrectPassword123!", "test-isolation"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(a.Token);
		AuthenticatedActor other = await fixture.Service.AuthenticateAsync(b.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await service.CreateAsync(actor, new CreateProjectRequest("Plan"));
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => service.DeleteAsync(actor, project.Id, 1, false))).StatusCode);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => service.DeleteAsync(other, project.Id, 1, true))).StatusCode);
		Assert.Equal(404, (await Assert.ThrowsAsync<PlanForgeException>(() => service.SaveAsync(other, project.Id, project))).StatusCode);
		Assert.Equal(409, (await Assert.ThrowsAsync<PlanForgeException>(() => service.DeleteAsync(actor, project.Id, 0, true))).StatusCode);
		await service.DeleteAsync(actor, project.Id, 1, true);
		Assert.Empty(await service.ListAsync(actor));
	}
}
