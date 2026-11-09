namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Planning;

public sealed class PlanExplorerTests
{
	[Fact]
	public async Task Explorer_AdvancesOnlyForLocalSelectedCompletionOrSuccessfulImportAsync()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), pending = Guid.NewGuid(), active = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 3, "Project", "", "InProgress", "PlanningOnly", DateTimeOffset.UtcNow,
			[new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"), new(pending, "Step", subphase, 0, "Pending", CreationOrder: 1), new(active, "Step", subphase, 1, "Active", Status: "InProgress", CreationOrder: 2)]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var tree = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project));
		project = project with { Revision = 4, Items = project.Items.Select(item => item.Id == active ? item with { Status = "Completed" } : item).ToList() };
		tree.Render(parameters => parameters.Add(component => component.Snapshot, project));
		Assert.Equal(active.ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		tree.Render(parameters => parameters.Add(component => component.Snapshot, project).Add(component => component.SelectionEpoch, 1).Add(component => component.CompletedItemId, active));
		Assert.Equal(pending.ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		await tree.Find($"button[data-select-id='{phase}']").ClickAsync(new());
		tree.Render(parameters => parameters.Add(component => component.Snapshot, project).Add(component => component.SelectionEpoch, 2).Add(component => component.CompletedItemId, pending));
		Assert.Equal(phase.ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		tree.Render(parameters => parameters.Add(component => component.Snapshot, project).Add(component => component.SelectionEpoch, 3).Add(component => component.CompletedItemId, (Guid?)null));
		Assert.Equal(pending.ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Explorer_DeepLinkSelectsOnlyAStepInsideAuthorizedSnapshot(bool valid)
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), pending = Guid.NewGuid(), active = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 3, "Project", "", "InProgress", "PlanningOnly", DateTimeOffset.UtcNow,
			[new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"), new(pending, "Step", subphase, 0, "Pending", CreationOrder: 1), new(active, "Step", subphase, 1, "Active", Status: "InProgress", CreationOrder: 2)]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var tree = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project).Add(component => component.RequestedStepId, valid ? pending : Guid.NewGuid()));
		Assert.Equal((valid ? pending : active).ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		Assert.Equal("true", tree.Find($"button[aria-controls='branch-{phase}']").GetAttribute("aria-expanded"));
	}

	[Fact]
	public async Task Explorer_RestoresSessionSelectionAndManualCollapseWithoutPollingJumpsAsync()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 3, "Project", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow,
			[new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"), new(step, "Step", subphase, 0, "Step", CreationOrder: 1)]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var tree = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project));
		await tree.Find($"button[data-select-id='{phase}']").ClickAsync(new());
		await tree.Find($"button[aria-controls='branch-{phase}']").ClickAsync(new());
		int scrolls = context.JSInterop.Invocations.Count(call => call.Identifier == "planForge.revealCurrentWork");
		tree.Render(parameters => parameters.Add(component => component.Snapshot, project with { Revision = 4 }));
		Assert.Equal(scrolls, context.JSInterop.Invocations.Count(call => call.Identifier == "planForge.revealCurrentWork"));
		Assert.Equal("false", tree.Find($"button[aria-controls='branch-{phase}']").GetAttribute("aria-expanded"));
		tree.Dispose();
		var restored = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project));
		Assert.Equal(phase.ToString(), restored.Find("#plan-inspector").GetAttribute("data-selected-id"));
		Assert.Equal("false", restored.Find($"button[aria-controls='branch-{phase}']").GetAttribute("aria-expanded"));
	}

	[Fact]
	public async Task Explorer_SelectsOldestInProgressThenLetsUserInspectAParentAsync()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), oldest = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 3, "Project", "Purpose", "InProgress", "PlanningOnly", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"),
			new(Guid.NewGuid(), "Step", subphase, 0, "Older pending", CreationOrder: 1),
			new(Guid.NewGuid(), "Step", subphase, 1, "Newer active", Status: "InProgress", CreationOrder: 3),
			new(oldest, "Step", subphase, 2, "Oldest active", Status: "InProgress", CreationOrder: 2)
		]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var tree = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project));
		Assert.Equal(oldest.ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		Assert.Empty(tree.FindAll(".work-plan-tree input, .work-plan-tree textarea, .node-body"));
		await tree.Find($"button[data-select-id='{phase}']").ClickAsync(new());
		Assert.Equal(phase.ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		Assert.Contains("Status updates automatically as work progresses.", tree.Find("#plan-inspector").TextContent);
		Assert.Single(tree.FindAll(".work-plan-tree [aria-current=true]"));
	}
}
