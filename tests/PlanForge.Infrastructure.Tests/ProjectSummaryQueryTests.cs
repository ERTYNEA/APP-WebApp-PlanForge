namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Planning;
using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;
using PlanForge.Infrastructure.Planning;

public sealed class ProjectSummaryQueryTests
{
	[Theory]
	[InlineData(-1, 1)]
	[InlineData(0, 0)]
	[InlineData(0, 101)]
	[InlineData(int.MaxValue, 1)]
	public async Task SummariesAsync_WhenPageBoundsInvalid_RejectsUnboundedQueriesAsync(int skip, int take)
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => service.ListSummariesAsync(actor, skip, take))).StatusCode);
	}

	[Fact]
	public async Task SummariesAsync_WhenPaged_AggregatesPersistedTreeWithoutReturningItsRecordsAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("a@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(service);
		await service.CreateAsync(actor, new("Same name"));
		var project = await service.CreateAsync(actor, new("Same name", new string('d', 500)));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Done", subphase, Status: "Completed", Evidence: "WorkDone"));
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Pending", subphase));
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Decision", subphase));
		var page = await service.ListSummariesAsync(actor, take: 1);
		Assert.Equal(2, page.Total);
		Assert.True(page.HasMore);
		var summary = Assert.Single(page.Items);
		Assert.Equal(project.Id, summary.Id);
		Assert.Equal(project.Revision, summary.Revision);
		Assert.Equal(240, summary.Description.Length);
		Assert.Equal(1, summary.PhaseCount);
		Assert.Equal(2, summary.StepCount);
		Assert.Equal(1, summary.CompletedSteps);
		Assert.Equal(1, summary.PendingDecisions);
		Assert.Equal(50, summary.Progress);
		Assert.True(summary.LatestChangeId > 0);
		Assert.False((await service.ListSummariesAsync(actor, 1, 1)).HasMore);
		Assert.Equal(0, (await service.ListSummariesAsync(actor with { UserId = Guid.NewGuid() })).Total);
		Assert.DoesNotContain(summary.GetType().GetProperties(), property => property.Name == "Items");
	}
}
