namespace PlanForge.Web.Tests;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Planning;

public sealed class WorkPlanTreeTests
{
	[Fact]
	public async Task Render_WhenActiveStepExists_ShowsActiveHierarchyDecisionAgentAndCollapsesFutureAsync()
	{
		Guid phase=Guid.NewGuid(), subphase=Guid.NewGuid(), step=Guid.NewGuid(), future=Guid.NewGuid(), futureSubphase=Guid.NewGuid(), agent=Guid.NewGuid();
		var project = new ProjectSnapshot(Guid.NewGuid(),3,"Project","Purpose","InProgress","LocalChangesManualApproval",DateTimeOffset.UtcNow,
		[
			new(phase,"Phase",null,0,"Active phase",Status:"InProgress",AssignmentMode:"Override",AssignedAgentId:agent),
			new(subphase,"Subphase",phase,0,"Active subphase",Status:"InProgress"),
			new(step,"Step",subphase,0,"Current actionable step",Status:"InProgress",Evidence:"WorkDone"),
			new(Guid.NewGuid(),"Decision",step,0,"Pending choice",Status:"Pending"),
			new(future,"Phase",null,1,"Future phase"),new(futureSubphase,"Subphase",future,0,"Future subphase"),new(Guid.NewGuid(),"Step",futureSubphase,0,"Future hidden step")
		]);
		var services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
		await using var renderer = new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
		string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<WorkPlanTree>(ParameterView.FromDictionary(new Dictionary<string,object?> { ["Snapshot"]=project,["Agents"]=new[]{new AgentProfile(agent,"NEA","",true,false)} }))).ToHtmlString());
		Assert.Contains("Current actionable step",html);
		Assert.Contains("Awaiting approval",html);
		Assert.Contains("Pending choice",html);
		Assert.Contains("NEA",html);
		Assert.Contains("Future phase",html);
		Assert.DoesNotContain("Future hidden step",html);
		Assert.Contains("aria-expanded=\"true\"",html);
	}
}
