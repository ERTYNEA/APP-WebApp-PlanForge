namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;

public sealed class AutomaticStatusTests
{
	[Fact]
	public async Task Cleanup_WhenAllPhasesCompleted_ReturnsEmptyPendingProjectAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("empty-cleanup@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(service);
		ProjectSnapshot project = await service.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Done", project.Items.Single(item => item.Kind == "Subphase").Id, Status: "Completed", Evidence: "WorkDone"));
		Assert.Equal("Completed", project.Status);
		long revision = project.Revision;
		project = await editor.CleanCompletedAsync(actor, project.Id, new(revision, true));
		Assert.Empty(project.Items);
		Assert.Equal("Pending", project.Status);
		Assert.Equal(revision + 1, project.Revision);
		Assert.Equal("Pending", (await service.GetAsync(actor, project.Id)).Status);
		Assert.Equal("Pending", (await service.ListSummariesAsync(actor)).Items.Single().Status);
	}

	[Fact]
	public async Task SaveAsync_RejectsStepCompletionWithUnresolvedLocalDecisionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("decision@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(service);
		ProjectSnapshot project = await service.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", subphase));
		Guid step = project.Items.Single(item => item.Kind == "Step").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Question", step));
		long revision = project.Revision;

		PlanForgeException error = await Assert.ThrowsAsync<PlanForgeException>(() => editor.SaveItemAsync(actor, project.Id, "Step", step, new(revision, "Step", subphase, Status: "Completed", Evidence: "WorkDone")));

		Assert.Equal(400, error.StatusCode);
		Assert.Equal(revision, (await service.GetAsync(actor, project.Id)).Revision);
	}

	[Fact]
	public async Task SaveAsync_IgnoresLegacyParentStatusAndPersistsDerivedStateAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("derived@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await service.CreateAsync(actor, new("Plan"));
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		project = await service.SaveAsync(actor, project.Id, project with
		{
			Status = "Completed",
			Items = [new(phase, "Phase", null, 0, "Phase", Status: "Completed"), new(subphase, "Subphase", phase, 0, "Subphase", Status: "Blocked")]
		});

		Assert.Equal("Pending", project.Status);
		Assert.All(project.Items, item => Assert.Equal("Pending", item.Status));
		Assert.Equal("Pending", (await service.ListSummariesAsync(actor)).Items.Single().Status);
		Assert.Equal(project.Status, (await service.GetAsync(actor, project.Id)).Status);
	}
}
