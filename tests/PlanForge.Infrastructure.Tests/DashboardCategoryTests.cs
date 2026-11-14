namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Infrastructure.Planning;
using System.Text.Json;

public sealed class DashboardCategoryTests
{
	[Fact]
	public async Task Dashboard_EmptyOwnerExposesAllFourZeroCategoriesAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("empty-dashboard@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		using var json = JsonSerializer.SerializeToDocument(await service.GetDashboardAsync(actor));
		foreach (string category in new[] { "Projects", "Phases", "Subphases", "Steps" })
		{
			Assert.True(json.RootElement.TryGetProperty(category, out JsonElement counts), "Dashboard must expose category " + category);
			foreach (string name in new[] { "Total", "Pending", "InProgress", "Blocked", "Completed" })
				Assert.Equal(0, counts.GetProperty(name).GetInt32());
		}
	}
	[Fact]
	public async Task Dashboard_CountsEffectiveStatesAtEveryLevelAndExcludesOtherOwnersAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("mixed-dashboard@example.test", "CorrectPassword123!", "test"));
		var otherGrant = await fixture.Service.RegisterAsync(new RegisterRequest("other-dashboard@example.test", "CorrectPassword123!", "other"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var other = await fixture.Service.AuthenticateAsync(otherGrant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(service);
		await service.CreateAsync(other, new("Other owner"));
		foreach (string status in new[] { "Pending", "InProgress", "Blocked", "Completed" })
		{
			var project = await service.CreateAsync(actor, new(status));
			project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
			Guid parent = project.Items.Single(item => item.Kind == "Subphase").Id;
			project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", parent, Status: status == "Blocked" ? "InProgress" : status, Evidence: status == "Completed" ? "WorkDone" : null));
			if (status == "Blocked")
				project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Question", project.Items.Single(item => item.Kind == "Step").Id));
		}
		var result = await service.GetDashboardAsync(actor);
		foreach (var counts in new[] { result.Projects, result.Phases, result.Subphases, result.Steps })
		{
			Assert.Equal(new PlanForge.Contracts.Planning.StatusCounts(1, 1, 1, 1), counts);
			Assert.Equal(4, counts.Total);
		}
		Assert.Equal(25, result.Progress);
		Assert.Equal(result.Steps.Total, result.TotalSteps);
		Assert.Equal(result.Projects.Total, result.ProjectCount);
		Assert.Equal(1, (await service.GetDashboardAsync(other)).Projects.Pending);
		Assert.Equal(0, (await service.GetDashboardAsync(other)).Steps.Total);
	}

	[Fact]
	public async Task Dashboard_LargeWorkspaceUsesThreeBoundedQueriesAndPreservesWeightedProgressAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("large-dashboard@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var project = await service.CreateAsync(actor, new("Large"));
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		var items = new List<PlanForge.Contracts.Planning.PlanItem> { new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase") };
		items.AddRange(Enumerable.Range(0, 2000).Select(index => new PlanForge.Contracts.Planning.PlanItem(Guid.NewGuid(), "Step", subphase, index, "Step " + index, Status: index % 2 == 0 ? "Completed" : "Pending", Evidence: index % 2 == 0 ? "WorkDone" : null)));
		await service.SaveAsync(actor, project.Id, project with { Items = items });
		var counter = new DashboardCommandCounter();
		await using var reader = new PlanForge.Infrastructure.Database.PlanForgeDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<PlanForge.Infrastructure.Database.PlanForgeDbContext>().UseSqlite(fixture.Database.Database.GetDbConnection()).AddInterceptors(counter).Options);
		var result = await new ProjectService(new EfProjectStore(reader), TimeProvider.System).GetDashboardAsync(actor);
		Assert.Equal(2000, result.Steps.Total);
		Assert.Equal(1000, result.Steps.Completed);
		Assert.Equal(1000, result.Steps.Pending);
		Assert.Equal(50, result.Progress);
		Assert.Equal(3, result.NextSteps.Count);
		Assert.Equal(3, counter.Queries);
	}

	private sealed class DashboardCommandCounter : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
	{
		public int Queries { get; private set; }
		public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
		{
			Queries++;
			return ValueTask.FromResult(result);
		}
	}

}
