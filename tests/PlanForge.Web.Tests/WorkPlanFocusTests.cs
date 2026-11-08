namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Planning;

public sealed class WorkPlanFocusTests
{
	[Fact]
	public void Render_WhenActiveBranchChanges_RequestsCurrentWorkScrollOnce()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid();
		var snapshot = new ProjectSnapshot(Guid.NewGuid(), 1, "Project", "", "InProgress", "PlanningOnly", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Phase", Status: "InProgress"),
			new(subphase, "Subphase", phase, 0, "Subphase", Status: "InProgress"),
			new(step, "Step", subphase, 0, "Current step", Status: "InProgress")
		]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		context.Render<WorkPlanTree>(parameters => parameters.Add(tree => tree.Snapshot, snapshot));
		var invocation = Assert.Single(context.JSInterop.Invocations, call => call.Identifier == "planForge.revealCurrentWork");
		Assert.Equal(step.ToString(), invocation.Arguments.Single());
	}

	[Fact]
	public async Task Render_WhenAssignedAgentHasAvatar_ShowsAuthenticatedAvatarRouteAsync()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), agent = Guid.NewGuid();
		var snapshot = new ProjectSnapshot(Guid.NewGuid(), 1, "Project", "", "InProgress", "PlanningOnly", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Phase", Status: "InProgress", AssignmentMode: "Override", AssignedAgentId: agent),
			new(subphase, "Subphase", phase, 0, "Subphase", Status: "InProgress")
		]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var component = context.Render<WorkPlanTree>(parameters => parameters
			.Add(tree => tree.Snapshot, snapshot)
			.Add(tree => tree.Agents, new[] { new PlanForge.Contracts.Agents.AgentProfile(agent, "Agent", "", true, true) }));
		Assert.Equal(snapshot.Id.ToString(), component.Find("#plan-inspector").GetAttribute("data-selected-id"));
		await component.Find($"button[data-select-id='{phase}']").ClickAsync(new());
		Assert.Contains($"/files/agent-avatars/{agent}", component.Markup);
	}

	[Fact]
	public void Render_WhenSubphaseActiveWithoutActiveStep_ExpandsRealActiveBranch()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid();
		var snapshot = new ProjectSnapshot(Guid.NewGuid(), 1, "Project", "", "InProgress", "PlanningOnly", DateTimeOffset.UtcNow,
		[
			new(phase, "Phase", null, 0, "Active phase", Status: "InProgress"),
			new(subphase, "Subphase", phase, 0, "Active subphase", Status: "InProgress"),
			new(Guid.NewGuid(), "Step", subphase, 0, "Pending actionable step")
		]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		var component = context.Render<WorkPlanTree>(parameters => parameters.Add(tree => tree.Snapshot, snapshot));
		Assert.Contains("Pending actionable step", component.Markup);
	}
}
