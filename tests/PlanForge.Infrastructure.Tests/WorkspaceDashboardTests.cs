namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;

public sealed class WorkspaceDashboardTests
{
	[Fact]
	public async Task Dashboard_SimultaneousCsvStepsUseHierarchyBeforeGuidAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("dashboard-ties@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		ProjectSnapshot project = await projects.CreateAsync(actor, new("Plan"));
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), otherPhase = Guid.NewGuid(), otherSubphase = Guid.NewGuid();
		Guid first = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), second = Guid.Parse("88888888-8888-8888-8888-888888888888"), third = Guid.Parse("00000000-0000-0000-0000-000000000001");
		project = await projects.SaveAsync(actor, project.Id, project with { Items = [new(phase, "Phase", null, 0, "First phase"), new(subphase, "Subphase", phase, 0, "First subphase"), new(otherPhase, "Phase", null, 1, "Other phase"), new(otherSubphase, "Subphase", otherPhase, 0, "Other subphase"), new(first, "Step", subphase, 0, "Tree first"), new(second, "Step", subphase, 1, "Tree second"), new(third, "Step", otherSubphase, 0, "Tree third")] });
		Assert.Single(project.Items.Select(item => item.CreationOrder).Distinct());
		WorkspaceDashboard dashboard = await projects.GetDashboardAsync(actor);
		Assert.Equal(new[] { first, second, third }, dashboard.NextSteps.Select(step => step.StepId));
	}

	[Fact]
	public async Task Dashboard_UsesOwnerScopedWeightedCountsAndMixedImmutableCreationOrderAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("dashboard@example.test", "CorrectPassword123!", "test"));
		var otherGrant = await fixture.Service.RegisterAsync(new RegisterRequest("other@example.test", "CorrectPassword123!", "other"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var other = await fixture.Service.AuthenticateAsync(otherGrant.Token);
		var projects = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(projects);
		await projects.CreateAsync(other, new("Private project"));
		ProjectSnapshot done = await projects.CreateAsync(actor, new("Done project"));
		done = await editor.SaveItemAsync(actor, done.Id, "Phase", null, new(done.Revision, "Done phase"));
		done = await editor.SaveItemAsync(actor, done.Id, "Step", null, new(done.Revision, "Done", done.Items.Single(item => item.Kind == "Subphase").Id, Status: "Completed", Evidence: "WorkDone"));
		ProjectSnapshot active = await projects.CreateAsync(actor, new("Active project"));
		active = await editor.SaveItemAsync(actor, active.Id, "Phase", null, new(active.Revision, "Phase name"));
		Guid subphase = active.Items.Single(item => item.Kind == "Subphase").Id;
		foreach ((string title, string status) in new[] { ("Oldest pending", "Pending"), ("Middle active", "InProgress"), ("Blocked", "Pending"), ("Newest pending", "Pending") })
			active = await editor.SaveItemAsync(actor, active.Id, "Step", null, new(active.Revision, title, subphase, Status: status));
		active = await editor.SaveItemAsync(actor, active.Id, "Decision", null, new(active.Revision, "Blocking question", active.Items.Single(item => item.Title == "Blocked").Id));
		Guid oldest = active.Items.Single(item => item.Title == "Oldest pending").Id;
		active = await editor.SaveItemAsync(actor, active.Id, "Step", oldest, new(active.Revision, "Oldest edited", subphase, SortOrder: 3));

		WorkspaceDashboard result = await projects.GetDashboardAsync(actor);

		Assert.Equal(2, result.ProjectCount);
		Assert.Equal(2, result.PendingSteps);
		Assert.Equal(1, result.CompletedSteps);
		Assert.Equal(5, result.TotalSteps);
		Assert.Equal(20, result.Progress);
		Assert.Equal(new[] { "Oldest edited", "Middle active", "Newest pending" }, result.NextSteps.Select(step => step.StepName));
		Assert.All(result.NextSteps, step => { Assert.Equal(active.Id, step.ProjectId); Assert.Equal(active.Revision, step.ProjectRevision); Assert.Equal("Phase name", step.PhaseName); Assert.Equal("Default subphase", step.SubphaseName); });
		Assert.Empty((await projects.GetDashboardAsync(other)).NextSteps);
		Assert.Equal(0, (await projects.GetDashboardAsync(other)).Progress);
	}
}
