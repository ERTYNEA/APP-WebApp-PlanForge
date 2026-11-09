namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Exchange;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Planning;

public sealed class PendingDecisionStateTests
{
	[Theory]
	[InlineData("Pending")]
	[InlineData("InProgress")]
	[InlineData("Completed")]
	public async Task MultipleDecisions_OnlyLastResolutionRestoresBaseAndEligibleDashboardAsync(string status)
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("many-decisions@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(service);
		ProjectSnapshot project = await service.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Step", subphase, Status: status, Evidence: status == "Completed" ? "WorkDone" : null));
		Guid step = project.Items.Single(item => item.Kind == "Step").Id;
		for (int index = 0; index < 2; index++)
			project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Question " + index, step));
		Assert.Equal("Blocked", project.Status);
		Assert.Equal(status, project.Items.Single(item => item.Id == step).Status);
		Assert.Empty((await service.GetDashboardAsync(actor)).NextSteps);
		Assert.Equal("Blocked", (await service.ListSummariesAsync(actor)).Items.Single().Status);
		foreach (Guid decision in project.Items.Where(item => item.Kind == "Decision").Select(item => item.Id).ToList())
		{
			project = await editor.DeleteItemAsync(actor, project.Id, "Decision", decision, new(project.Revision, true));
			Assert.Equal(project.Items.Any(item => item.Kind == "Decision") ? "Blocked" : status, project.Items.Single(item => item.Id == step).EffectiveStatus);
		}
		Assert.Equal(status, project.Status);
		Assert.Equal(status == "Completed" ? 0 : 1, (await service.GetDashboardAsync(actor)).NextSteps.Count);
	}

	[Fact]
	public async Task ManualBlocked_IsRejectedByEditorSnapshotAndCsvWithoutRevisionChangeAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("manual-blocked@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(service);
		ProjectSnapshot project = await service.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		PlanForgeException error = await Assert.ThrowsAsync<PlanForgeException>(() => editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Blocked step", subphase, Status: "Blocked")));
		Assert.Equal(400, error.StatusCode);
		Guid step = Guid.NewGuid();
		ProjectSnapshot invalid = project with { Items = project.Items.Append(new PlanItem(step, "Step", subphase, 0, "Step", Status: "Blocked")).ToList() };
		Assert.Equal(400, (await Assert.ThrowsAsync<PlanForgeException>(() => service.SaveAsync(actor, project.Id, invalid))).StatusCode);
		ProjectSnapshot valid = invalid with { Items = invalid.Items.Select(item => item.Id == step ? item with { Status = "Pending" } : item).ToList() };
		string csv = System.Text.Encoding.UTF8.GetString(WorkPlanCsvCodec.Serialize(valid)).Replace(";Step;;Pending;", ";Step;;Blocked;", StringComparison.Ordinal);
		Assert.Equal(400, Assert.Throws<PlanForgeException>(() => WorkPlanCsvCodec.Parse(System.Text.Encoding.UTF8.GetBytes(csv))).StatusCode);
		Assert.Equal(project.Revision, (await service.GetAsync(actor, project.Id)).Revision);
	}

	[Fact]
	public async Task RootDecision_BlocksOnlyProjectAndSurvivesCsvAndDeletionAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("root-decision@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(service);
		ProjectSnapshot project = await service.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Root question", project.Id));
		Assert.Equal("Blocked", project.Status);
		PlanItem decision = Assert.Single(project.Items);
		Assert.Equal(project.Id, decision.ParentId);
		Assert.Equal(project.Id, (await service.GetAsync(actor, project.Id)).Items.Single().ParentId);
		project = await service.SaveAsync(actor, project.Id, WorkPlanCsvCodec.Parse(WorkPlanCsvCodec.Serialize(project)));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Done", subphase, Status: "Completed", Evidence: "WorkDone"));
		Assert.Equal("Blocked", project.Status);
		Assert.All(project.Items.Where(item => item.Kind != "Decision"), item => Assert.Equal("Completed", item.EffectiveStatus));
		project = await editor.DeleteItemAsync(actor, project.Id, "Decision", decision.Id, new(project.Revision, true));
		Assert.Equal("Completed", project.Status);
	}

	[Fact]
	public async Task PendingDecision_PreservesCompletedStepBaseAndEvidenceAcrossResolutionAndCsvAsync()
	{
		await using var fixture = new AccountDatabaseFixture();
		var grant = await fixture.Service.RegisterAsync(new RegisterRequest("decision-base@example.test", "CorrectPassword123!", "test"));
		AuthenticatedActor actor = await fixture.Service.AuthenticateAsync(grant.Token);
		var service = new ProjectService(new EfProjectStore(fixture.Database), TimeProvider.System);
		var editor = new PlanEditor(service);
		ProjectSnapshot project = await service.CreateAsync(actor, new("Plan"));
		project = await editor.SaveItemAsync(actor, project.Id, "Phase", null, new(project.Revision, "Phase"));
		Guid subphase = project.Items.Single(item => item.Kind == "Subphase").Id;
		project = await editor.SaveItemAsync(actor, project.Id, "Step", null, new(project.Revision, "Done", subphase, Status: "Completed", Evidence: "WorkDone"));
		PlanItem completed = project.Items.Single(item => item.Kind == "Step");
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", null, new(project.Revision, "Question", completed.Id));
		PlanItem blocked = (await service.GetAsync(actor, project.Id)).Items.Single(item => item.Id == completed.Id);
		Assert.Equal("Completed", blocked.Status);
		Assert.Contains("\"EffectiveStatus\":\"Blocked\"", System.Text.Json.JsonSerializer.Serialize(blocked));
		Assert.Equal(completed.Evidence, blocked.Evidence);
		Assert.Equal(completed.CompletedAt, blocked.CompletedAt);
		Assert.Equal(0, project.Progress);
		Assert.Empty((await service.GetDashboardAsync(actor)).NextSteps);
		project = await service.SaveAsync(actor, project.Id, project with { Workflow = "DirectToMain" });
		Assert.Equal(completed.CompletedAt, project.Items.Single(item => item.Id == completed.Id).CompletedAt);
		ProjectSnapshot csv = WorkPlanCsvCodec.Parse(WorkPlanCsvCodec.Serialize(project));
		Assert.Equal("Completed", csv.Items.Single(item => item.Id == completed.Id).Status);
		project = await service.SaveAsync(actor, project.Id, csv);
		PlanItem decision = project.Items.Single(item => item.Kind == "Decision");
		project = await editor.SaveItemAsync(actor, project.Id, "Decision", decision.Id, new(project.Revision, decision.Title, completed.Id, Status: "Decided", Resolution: "Resolved"));
		Assert.Equal("Completed", project.Status);
		Assert.Equal(completed.CompletedAt, project.Items.Single(item => item.Id == completed.Id).CompletedAt);
		Assert.Equal(100, project.Progress);
	}

	[Theory]
	[InlineData("Phase")]
	[InlineData("Subphase")]
	[InlineData("Step")]
	public void Recalculate_PendingDecisionBlocksItsScopeAndAncestors(string scope)
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid();
		var snapshot = new ProjectSnapshot(Guid.NewGuid(), 1, "Plan", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"),
			new(step, "Step", subphase, 0, "Step", Status: "InProgress"),
			new(Guid.NewGuid(), "Decision", scope == "Phase" ? phase : scope == "Subphase" ? subphase : step, 0, "Question")
		]);
		ProjectSnapshot result = PlanProgress.Recalculate(snapshot);
		Assert.Equal("Blocked", result.Status);
		Assert.Equal("Blocked", result.Items.Single(item => item.Id == phase).Status);
		Assert.Equal(scope == "Phase" ? "InProgress" : "Blocked", result.Items.Single(item => item.Id == subphase).Status);
		Assert.Equal("InProgress", result.Items.Single(item => item.Id == step).Status);
		Assert.Equal(result.Items, PlanProgress.Recalculate(result).Items);
		Assert.Equal(result.Status, PlanProgress.Recalculate(result).Status);
	}
}
