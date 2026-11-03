namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;

public sealed class PlanEditorTests
{
	[Fact]
	public async Task SaveItemAsync_WhenStepCompletedThenReopened_DerivesParentProgressWithoutPrematureCompletionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new PlanItemCommand(project.Revision, "Phase"));
		PlanItem subphase = project.Items.Single(item => item.Kind == "Subphase");
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new PlanItemCommand(project.Revision, "Step", subphase.Id));
		PlanItem step = project.Items.Single(item => item.Kind == "Step");
		project = await editor.SaveItemAsync(actor, project.Id, "Step", step.Id, new PlanItemCommand(project.Revision, "Step", subphase.Id, Status: "Completed", Evidence: "WorkDone"));
		Assert.Equal(100, project.Progress);
		Assert.All(project.Items, item => Assert.Equal("Completed", item.Status));
		Assert.Equal("Completed", project.Status);
		project = await editor.SaveItemAsync(actor, project.Id, "Step", step.Id, new PlanItemCommand(project.Revision, "Step", subphase.Id));
		Assert.Equal(0, project.Progress);
		Assert.All(project.Items, item => Assert.NotEqual("Completed", item.Status));
		Assert.NotEqual("Completed", project.Status);
	}

	[Fact]
	public async Task SaveItemAsync_WhenDecisionDecided_PreservesResolutionActorAndDistinctDecisionStatusAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new PlanItemCommand(project.Revision, "Phase"));
		Guid phaseId = project.Items.Single(item => item.Kind == "Phase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new PlanItemCommand(project.Revision, "Storage?", phaseId, Status: "Decided", Resolution: "Use existing SQL"));
		PlanItem decision = project.Items.Single(item => item.Kind == "Decision");
		Assert.Equal("Decided", decision.Status);
		Assert.Equal("Use existing SQL", decision.Resolution);
		Assert.Equal(actor.UserId, decision.CompletedBy);
		Assert.NotNull(decision.CompletedAt);
		Assert.Equal(0, project.Progress);
	}

	[Fact]
	public async Task DeleteItemAsync_WhenLastSubphaseOrUnconfirmed_PreservesTreeAndAllowsConfirmedPhaseDeletionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new PlanItemCommand(project.Revision, "Phase"));
		Guid phase = project.Items.Single(item => item.Kind == "Phase").Id;
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => editor.DeleteItemAsync(actor, project.Id, "Subphase", subphase, new(project.Revision, true)))).StatusCode);
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => editor.DeleteItemAsync(actor, project.Id, "Phase", phase, new(project.Revision, false)))).StatusCode);
		project = await editor.DeleteItemAsync(actor, project.Id, "Phase", phase, new(project.Revision, true));
		Assert.Empty(project.Items);
		Assert.Empty(fixture.Database.WorkItems);
	}

	[Fact]
	public async Task CleanCompletedAsync_WhenMixedPhases_RemovesOnlyActuallyCompletedTreesAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Completed phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Done", subphase, Status: "Completed", Evidence: "WorkDone"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Pending phase"));
		long revision = project.Revision;
		project = await editor.CleanCompletedAsync(actor, project.Id, new(revision, true));
		Assert.Equal(revision + 1, project.Revision);
		Assert.Equal("Pending phase", project.Items.Single(item => item.Kind == "Phase").Title);
		Assert.Equal(2, project.Items.Count);
	}

	[Fact]
	public async Task CleanCompletedAsync_WhenStepHasPendingDecision_PreservesPhaseAndShowsIncompleteProgressAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", subphase, Status: "Completed", Evidence: "WorkDone"));
		Guid step = project.Items.Single(item => item.Kind == "Step").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Pending decision", step));
		Assert.NotEqual("Completed", project.Status);
		Assert.True(project.Progress < 100);
		project = await editor.CleanCompletedAsync(actor, project.Id, new(project.Revision, true));
		Assert.Equal(4, project.Items.Count);
	}

	[Fact]
	public async Task SaveItemAsync_WhenPhaseCreated_AddsRealEditableDefaultSubphaseAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new CreateProjectRequest("Plan"));
		var editor = new PlanEditor(projects);

		ProjectSnapshot saved = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new PlanItemCommand(project.Revision, "First phase"));

		PlanItem phase = Assert.Single(saved.Items, item => item.Kind == "Phase");
		PlanItem subphase = Assert.Single(saved.Items, item => item.Kind == "Subphase");
		Assert.Equal(phase.Id, subphase.ParentId);
		Assert.Equal("Default subphase", subphase.Title);
		ProjectSnapshot renamed = await editor.SaveItemAsync(actor, project.Id, "Subphase", subphase.Id, new PlanItemCommand(saved.Revision, "Renamed", phase.Id));
		Assert.Equal("Renamed", renamed.Items.Single(item => item.Id == subphase.Id).Title);
	}
}
