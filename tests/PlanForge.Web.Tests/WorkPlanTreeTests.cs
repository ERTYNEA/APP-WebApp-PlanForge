namespace PlanForge.Web.Tests;

using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Planning;

public sealed class WorkPlanTreeTests
{
	[Fact]
	public void Render_WhenActiveStepExists_ShowsActiveHierarchyDecisionAgentAndCollapsesFuture()
	{
		Guid phase = Guid.NewGuid(), subphase = Guid.NewGuid(), step = Guid.NewGuid(), future = Guid.NewGuid(), futureSubphase = Guid.NewGuid(), agent = Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(), 3, "Project", "Purpose", "InProgress", "LocalChangesManualApproval", DateTimeOffset.UtcNow,
		[
			new(phase,"Phase",null,0,"Active phase",Status:"InProgress",AssignmentMode:"Override",AssignedAgentId:agent),
			new(subphase,"Subphase",phase,0,"Active subphase",Status:"InProgress"),
			new(step,"Step",subphase,0,"Current actionable step",Status:"InProgress",Evidence:"WorkDone"),
			new(Guid.NewGuid(),"Decision",step,0,"Pending choice",Status:"Pending"),
			new(future,"Phase",null,1,"Future phase"),new(futureSubphase,"Subphase",future,0,"Future subphase"),new(Guid.NewGuid(),"Step",futureSubphase,0,"Future hidden step")
		]);
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API call expected."));
		string html = context.Render<WorkPlanTree>(parameters => parameters
			.Add(tree => tree.Snapshot, project)
			.Add(tree => tree.Agents, new[] { new AgentProfile(agent, "NEA", "", true, false) })).Markup;
		Assert.Contains("Current actionable step", html);
		Assert.Contains("Awaiting approval", html);
		Assert.Contains("Pending choice", html);
		Assert.Contains("NEA", html);
		Assert.Contains("Future phase", html);
		Assert.DoesNotContain("Future hidden step", html);
		Assert.Contains("aria-expanded=\"true\"", html);
	}
}
