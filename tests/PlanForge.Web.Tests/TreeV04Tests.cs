namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Planning;

public sealed class TreeV04Tests
{
	[Fact]
	public void PassiveDeletion_SelectsSurvivingAncestorWithoutScrollingToAnotherStep()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid(), other = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 3, "Plan", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, [new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"), new(step, "Step", subphase, 0, "Selected"), new(other, "Step", subphase, 1, "Other")]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var tree = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project).Add(component => component.RequestedStepId, step));
		int scrolls = context.JSInterop.Invocations.Count(call => call.Identifier == "planForge.revealCurrentWork");
		tree.Render(parameters => parameters.Add(component => component.Snapshot, project with { Revision = 4, Items = project.Items.Where(item => item.Id != step).ToList() }));
		Assert.Equal(subphase.ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		Assert.Equal(scrolls, context.JSInterop.Invocations.Count(call => call.Identifier == "planForge.revealCurrentWork"));
	}

	[Fact]
	public async Task RestoredSelection_DoesNotReopenDeliberatelyCollapsedAncestorsAsync()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 3, "Plan", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, [new(phase, "Phase", null, 0, "Phase"), new(subphase, "Subphase", phase, 0, "Subphase"), new(step, "Step", subphase, 0, "Step")]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var tree = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project));
		await tree.Find("button[data-plan-action='collapse-all']").ClickAsync(new());
		tree.Dispose();
		var restored = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project));
		Assert.Equal(step.ToString(), restored.Find("#plan-inspector").GetAttribute("data-selected-id"));
		Assert.True(restored.Find("button[data-plan-action='collapse-all']").HasAttribute("disabled"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void DefaultFocus_UsesEffectiveStateAndHierarchyBeforeGuidForSimultaneousCreation(bool firstBlocked)
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), otherPhase = Guid.NewGuid(), otherSubphase = Guid.NewGuid();
		Guid first = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), second = Guid.Parse("88888888-8888-8888-8888-888888888888"), third = Guid.Parse("00000000-0000-0000-0000-000000000001");
		var project = new ProjectSnapshot(Guid.NewGuid(), 4, "Plan", "", "InProgress", "PlanningOnly", DateTimeOffset.UtcNow,
			[new(phase, "Phase", null, 0, "First phase"), new(subphase, "Subphase", phase, 0, "First subphase"), new(otherPhase, "Phase", null, 1, "Other phase"), new(otherSubphase, "Subphase", otherPhase, 0, "Other subphase"), new(first, "Step", subphase, 0, "Tree first", Status: "InProgress", CreationOrder: 1) { EffectiveStatus = firstBlocked ? "Blocked" : "InProgress" }, new(second, "Step", subphase, 1, "Tree second", Status: "InProgress", CreationOrder: 1), new(third, "Step", otherSubphase, 0, "Tree third", Status: "InProgress", CreationOrder: 1)]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var tree = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project));
		Assert.Equal((firstBlocked ? second : first).ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		if (firstBlocked)
		{
			Assert.Equal("Blocked", tree.Find($"li[data-item-id='{first}'] .badge").TextContent);
			Assert.Equal("Blocked", PlanForge.Web.PlanLabels.Status(project.Items.Single(item => item.Id == first), project.Workflow));
		}
	}

	[Fact]
	public async Task ExpandCollapseAll_PreserveSelectionAndDoNotScrollOrResetOnPassiveRefreshAsync()
	{
		Guid first = Guid.NewGuid(), second = Guid.NewGuid(), subphase = Guid.NewGuid(), other = Guid.NewGuid(), step = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 3, "Plan", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow,
			[new(first, "Phase", null, 0, "First"), new(second, "Phase", null, 1, "Second"), new(subphase, "Subphase", first, 0, "Subphase"), new(other, "Subphase", second, 0, "Other"), new(step, "Step", subphase, 0, "Step")]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var tree = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, project));
		int scrolls = context.JSInterop.Invocations.Count(call => call.Identifier == "planForge.revealCurrentWork");
		var expand = tree.Find("button[data-plan-action='expand-all']");
		Assert.False(expand.HasAttribute("disabled"));
		await expand.ClickAsync(new());
		Assert.True(tree.Find("button[data-plan-action='expand-all']").HasAttribute("disabled"));
		Assert.All(tree.FindAll("button[aria-expanded]"), button => Assert.Equal("true", button.GetAttribute("aria-expanded")));
		Assert.Equal(step.ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		await tree.Find("button[data-plan-action='collapse-all']").ClickAsync(new());
		Assert.True(tree.Find("button[data-plan-action='collapse-all']").HasAttribute("disabled"));
		Assert.All(tree.FindAll("button[aria-expanded]"), button => Assert.Equal("false", button.GetAttribute("aria-expanded")));
		tree.Render(parameters => parameters.Add(component => component.Snapshot, project with { Revision = 4 }));
		Assert.Equal(step.ToString(), tree.Find("#plan-inspector").GetAttribute("data-selected-id"));
		Assert.Equal(scrolls, context.JSInterop.Invocations.Count(call => call.Identifier == "planForge.revealCurrentWork"));
		Assert.All(tree.FindAll("button[aria-expanded]"), button => Assert.Equal("false", button.GetAttribute("aria-expanded")));
		Assert.DoesNotContain("Add phase", tree.Find(".work-plan-heading").TextContent);
		Assert.Equal(new[] { "⇊ Expand all", "⇈ Collapse all", "↻ Reload" }, tree.FindAll(".work-plan-heading button").Select(button => button.TextContent.Trim()));
	}

	[Fact]
	public void EmptyPlan_DisablesBothGlobalExpansionActionsAndKeepsRootCreation()
	{
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var tree = context.Render<WorkPlanTree>(parameters => parameters.Add(component => component.Snapshot, new ProjectSnapshot(Guid.NewGuid(), 1, "Empty", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, [])));
		Assert.All(tree.FindAll("button[data-plan-action]"), button => Assert.True(button.HasAttribute("disabled")));
		Assert.Equal(2, tree.FindAll("button[data-plan-action]").Count);
		Assert.Contains("Add phase", tree.Find(".inspector-actions").TextContent);
	}
}
