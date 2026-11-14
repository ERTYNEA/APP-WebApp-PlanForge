namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Infrastructure.Planning;
using System.Text.Json;

public sealed class EffectiveStatusDateTests
{
	[Fact]
	public async Task CreateAsync_RecordsTheInitialEffectiveStateDateAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("status-date@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var clock = new StatusClock();
		var service = new ProjectService(new EfProjectStore(fixture.Database), clock);
		var project = await service.CreateAsync(actor, new("Initial state"));
		using JsonDocument json = JsonSerializer.SerializeToDocument(await service.GetAsync(actor, project.Id));
		Assert.True(json.RootElement.TryGetProperty("EffectiveStatusChangedAt", out JsonElement date), "The API must expose the persisted initial effective-state date.");
		Assert.Equal(clock.GetUtcNow(), date.GetDateTimeOffset());
	}

	[Fact]
	public async Task SaveAsync_InitialNodesAndRealTransitionsUseOneClockInstantAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("node-date@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var clock = new StatusClock();
		var service = new ProjectService(new EfProjectStore(fixture.Database), clock);
		var project = await service.CreateAsync(actor, new("State changes"));
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid();
		clock.Now = clock.Now.AddHours(1);
		project = await service.SaveAsync(actor, project.Id, project with { Items = [new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"), new(step, "Step", subphase, 0, "Step")] });
		Assert.All(project.Items, item => Assert.Equal(clock.Now, Date(item)));
		Assert.Equal(clock.Now.AddHours(-1), Date(project));
		clock.Now = clock.Now.AddHours(1);
		project = await service.SaveAsync(actor, project.Id, project with { Items = project.Items.Select(item => item.Id == step ? item with { Status = "InProgress" } : item).ToList() });
		Assert.Equal("InProgress", project.Status);
		Assert.Equal(clock.Now, Date(project));
		Assert.All(project.Items, item => Assert.Equal(clock.Now, Date(item)));
		Assert.Equal(Date(project), Date(await service.GetAsync(actor, project.Id)));
		Assert.All((await service.GetAsync(actor, project.Id)).Items, item => Assert.Equal(clock.Now, Date(item)));
	}

	[Theory]
	[InlineData("Project")]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public async Task Decisions_OnlyChangedAncestorsReceiveNewDatesAsync(string scope)
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("decision-date@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var clock = new StatusClock();
		var service = new ProjectService(new EfProjectStore(fixture.Database), clock);
		var editor = new PlanEditor(service);
		var project = await service.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid phase = project.Items.Single(item => item.Kind == "Phase").Id;
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", subphase, Status: "InProgress"));
		Guid step = project.Items.Single(item => item.Kind == "Step").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Sibling", subphase));
		var original = project.Items.ToDictionary(item => item.Id, Date);
		Guid target = scope switch { "Phase" => phase, "Subphase" => subphase, "Step" => step, _ => project.Id };
		clock.Now = clock.Now.AddHours(1);
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Question", target));
		DateTimeOffset blockedAt = clock.Now;
		var changedIds = project.Items.Where(item => item.EffectiveStatus == "Blocked").Select(item => item.Id).ToHashSet();
		Assert.Equal(blockedAt, Date(project));
		foreach (var item in project.Items)
			Assert.Equal(item.Kind == "Decision" ? null : item.EffectiveStatus == "Blocked" ? blockedAt : original[item.Id], Date(item));
		clock.Now = clock.Now.AddHours(1);
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Another", target, SortOrder: 1));
		Assert.Equal(blockedAt, Date(project));
		if (scope == "Step")
		{
			project = await editor.SaveItemAsync(actor, project.Id, "Step", step, new(project.Revision, "Edited base", subphase, Status: "Pending"));
			Assert.Equal(blockedAt, Date(project.Items.Single(item => item.Id == step)));
		}
		Guid first = project.Items.First(item => item.Kind == "Decision").Id;
		project = await editor.DeleteItemAsync(actor, project.Id, "Decision", first, new(project.Revision, true));
		Assert.Equal(blockedAt, Date(project));
		clock.Now = clock.Now.AddHours(1);
		Guid last = project.Items.Single(item => item.Kind == "Decision").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", last, new(project.Revision, "Resolved", target, SortOrder: 0, Status: "Decided", Resolution: "Answer"));
		Assert.Equal(clock.Now, Date(project));
		foreach (var item in project.Items.Where(item => item.Kind != "Decision"))
			Assert.Equal(changedIds.Contains(item.Id) ? clock.Now : original[item.Id], Date(item));
		Assert.Equal(original[project.Items.Single(item => item.Title == "Sibling").Id], Date(project.Items.Single(item => item.Title == "Sibling")));
	}

	[Fact]
	public async Task SnapshotAndCsv_IgnoreForgedDatesPreserveUnchangedAndStampReversalsAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("csv-date@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var clock = new StatusClock();
		var store = new EfProjectStore(fixture.Database);
		var service = new ProjectService(store, clock);
		var editor = new PlanEditor(service);
		var project = await service.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid parent = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", parent));
		Guid step = project.Items.Single(item => item.Kind == "Step").Id;
		foreach (string status in new[] { "InProgress", "Pending", "Completed", "InProgress", "Completed" })
		{
			clock.Now = clock.Now.AddHours(1);
			project = await editor.SaveItemAsync(actor, project.Id, "Step", step, new(project.Revision, "Step", parent, Status: status, Evidence: status == "Completed" ? "WorkDone" : null));
			Assert.Equal(clock.Now, Date(project));
			Assert.All(project.Items, item => Assert.Equal(clock.Now, Date(item)));
		}
		var completedAt = project.Items.Single(item => item.Id == step).CompletedAt;
		var dates = project.Items.ToDictionary(item => item.Id, Date);
		var rootDate = Date(project);
		clock.Now = clock.Now.AddDays(1);
		project = await service.SaveAsync(actor, project.Id, project with { Name = "Renamed", Description = "Changed", EffectiveStatusChangedAt = DateTimeOffset.MinValue, Items = project.Items.Select(item => item with { Title = "Renamed " + item.Kind, Description = "New description", EffectiveStatusChangedAt = DateTimeOffset.MaxValue }).ToList() });
		Assert.Equal(rootDate, Date(project));
		Assert.All(project.Items, item => Assert.Equal(dates[item.Id], Date(item)));
		var exchange = new PlanForge.Application.Exchange.WorkPlanExchange(service, store);
		byte[] csv = await exchange.ExportAsync(actor, project.Id);
		Assert.DoesNotContain("EffectiveStatusChangedAt", System.Text.Encoding.UTF8.GetString(csv));
		project = await exchange.ImportAsync(actor, project.Id, csv);
		Assert.Equal(rootDate, Date(project));
		Assert.All(project.Items, item => Assert.Equal(dates[item.Id], Date(item)));
		Assert.Equal(completedAt, project.Items.Single(item => item.Id == step).CompletedAt);
		var stale = project;
		clock.Now = clock.Now.AddHours(1);
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "New pending", parent));
		Assert.Equal("InProgress", project.Status);
		Assert.Equal(clock.Now, Date(project));
		Assert.Equal(409, (await Assert.ThrowsAsync<PlanForge.Application.Common.PlanForgeException>(() => service.SaveAsync(actor, project.Id, stale))).StatusCode);
		Assert.Equal(Date(project), Date(await service.GetAsync(actor, project.Id)));
		clock.Now = clock.Now.AddHours(1);
		project = await editor.DeleteItemAsync(actor, project.Id, "Phase", project.Items.Single(item => item.Kind == "Phase").Id, new(project.Revision, true));
		Assert.Equal("Pending", project.Status);
		Assert.Equal(clock.Now, Date(project));
	}

	[Fact]
	public async Task LegacyNulls_RemainUnknownUntilAnEffectiveTransitionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("legacy-date@example.test", "CorrectPassword123!", "test"));
		var actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var clock = new StatusClock();
		var service = new ProjectService(new EfProjectStore(fixture.Database), clock);
		var editor = new PlanEditor(service);
		var project = await service.CreateAsync(actor, new("Legacy"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		await fixture.Database.Projects.ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EffectiveStatusChangedAt, (DateTimeOffset?)null));
		await fixture.Database.WorkItems.ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EffectiveStatusChangedAt, (DateTimeOffset?)null));
		project = await service.GetAsync(actor, project.Id);
		project = await service.SaveAsync(actor, project.Id, project with { Description = "Not a state transition" });
		Assert.Null(Date(project));
		Assert.All(project.Items, item => Assert.Null(Date(item)));
		clock.Now = clock.Now.AddHours(1);
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Root question", project.Id));
		Assert.Equal(clock.Now, Date(project));
		Assert.All(project.Items, item => Assert.Null(Date(item)));
	}

	private static DateTimeOffset? Date(object value)
	{
		using JsonDocument json = JsonSerializer.SerializeToDocument(value);
		Assert.True(json.RootElement.TryGetProperty("EffectiveStatusChangedAt", out JsonElement date), "Effective-state date must be present in the contract.");
		return date.ValueKind == JsonValueKind.Null ? null : date.GetDateTimeOffset();
	}

	private sealed class StatusClock : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = new(2026, 11, 14, 1, 0, 0, TimeSpan.Zero);
		public override DateTimeOffset GetUtcNow() => Now;
	}
}
