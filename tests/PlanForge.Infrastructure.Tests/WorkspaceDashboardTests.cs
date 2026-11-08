namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;

public sealed class WorkspaceDashboardTests
{
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
		foreach ((string title, string status) in new[] { ("Oldest pending", "Pending"), ("Middle active", "InProgress"), ("Blocked", "Blocked"), ("Newest pending", "Pending") })
			active = await editor.SaveItemAsync(actor, active.Id, "Step", null, new(active.Revision, title, subphase, Status: status));
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
