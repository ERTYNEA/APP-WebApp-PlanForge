namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;
using System.Net.Http.Json;

public sealed class ProjectEditorTests
{
	[Theory]
	[InlineData("Subphase","Add subphase","Phase")]
	[InlineData("Step","Add step","Subphase")]
	[InlineData("Decision","Add decision","Step")]
	public async Task AddChild_WhenSubmitted_UsesSelectedParentAndTypedRouteAsync(string kind,string buttonText,string parentKind)
	{
		Guid phase=Guid.NewGuid(),subphase=Guid.NewGuid(),step=Guid.NewGuid();
		PlanItem[] items=[new(phase,"Phase",null,0,"Phase title",Status:"InProgress"),new(subphase,"Subphase",phase,0,"Subphase title",Status:"InProgress"),new(step,"Step",subphase,0,"Step title",Status:"InProgress")];
		var project=new ProjectSnapshot(Guid.NewGuid(),3,"Project","","InProgress","PlanningOnly",DateTimeOffset.UtcNow,items);
		PlanItemCommand? sent=null;
		string? sentPath=null;
		using var context=new ComponentTestContext(async request=>
		{
			string path=request.RequestUri!.AbsolutePath;
			if(request.Method==HttpMethod.Post) { sentPath=path; sent=await request.Content!.ReadFromJsonAsync<PlanItemCommand>(); }
			return ComponentTestContext.Json(path.EndsWith("/agents",StringComparison.Ordinal) ? (object)Array.Empty<AgentProfile>() : path.EndsWith("/resources",StringComparison.Ordinal) ? Array.Empty<ResourceProfile>() : path.EndsWith("/changes",StringComparison.Ordinal) ? new ProjectChangePage(project.Revision,1,false,[]) : project);
		});
		var page=context.Render<ProjectDetail>(parameters=>parameters.Add(component=>component.Id,project.Id));
		Guid parent=items.Single(item=>item.Kind==parentKind).Id;
		var parentNode=page.Find($"section[data-item-id='{parent}']");
		await parentNode.QuerySelectorAll("button").Last(button=>button.TextContent.Contains(buttonText,StringComparison.Ordinal)).ClickAsync(new());
		page.Find("#item-title").Change("Created child");
		await page.Find("dialog form").SubmitAsync();
		Assert.NotNull(sent);
		Assert.Equal(parent,sent.ParentId);
		Assert.Equal($"/api/v1/projects/{project.Id}/{PlanForge.Web.PlanLabels.RouteKind(kind)}",sentPath);
	}

	[Theory]
	[InlineData("Phase","phases")]
	[InlineData("Subphase","subphases")]
	[InlineData("Step","steps")]
	[InlineData("Decision","decisions")]
	public async Task Edit_WhenSubmitted_PreservesIdentityParentAndSendsClosureAssignmentAndOrderAsync(string kind,string route)
	{
		Guid phase=Guid.NewGuid(),subphase=Guid.NewGuid(),step=Guid.NewGuid(),decision=Guid.NewGuid(),agent=Guid.NewGuid();
		PlanItem[] items=[new(phase,"Phase",null,0,"Phase title",Status:"InProgress"),new(subphase,"Subphase",phase,0,"Subphase title",Status:"InProgress"),new(step,"Step",subphase,0,"Step title",Status:"InProgress"),new(decision,"Decision",step,0,"Decision title")];
		var project=new ProjectSnapshot(Guid.NewGuid(),3,"Project","","InProgress","LocalChangesManualApproval",DateTimeOffset.UtcNow,items);
		PlanItem target=items.Single(item=>item.Kind==kind);
		PlanItemCommand? sent=null;
		string? sentPath=null;
		using var context=new ComponentTestContext(async request=>
		{
			string path=request.RequestUri!.AbsolutePath;
			if(request.Method==HttpMethod.Put)
			{
				sentPath=path;
				sent=await request.Content!.ReadFromJsonAsync<PlanItemCommand>();
				project=project with {Revision=4};
			}
			return ComponentTestContext.Json(path.EndsWith("/agents",StringComparison.Ordinal) ? (object)new[]{new AgentProfile(agent,"NEA","",true,false)} : path.EndsWith("/resources",StringComparison.Ordinal) ? Array.Empty<ResourceProfile>() : path.EndsWith("/changes",StringComparison.Ordinal) ? new ProjectChangePage(project.Revision,1,false,[]) : project);
		});
		var page=context.Render<ProjectDetail>(parameters=>parameters.Add(component=>component.Id,project.Id));
		await page.Find($"button[aria-label='Edit {kind.ToLowerInvariant()} {target.Title}']").ClickAsync(new());
		page.Find("#item-title").Change("Edited title");
		page.Find("#item-order").Change("2");
		page.Find("#item-status").Change(kind=="Decision" ? "Decided" : "Completed");
		if(kind=="Decision") page.Find("#item-resolution").Change("Verified decision");
		else
		{
			page.Find("#item-assignment").Change("Override");
			page.Find("#item-agent").Change(agent.ToString());
			if(kind=="Step") page.Find("#item-evidence").Change("OwnerApproved");
		}
		await page.Find("dialog form").SubmitAsync();
		Assert.NotNull(sent);
		Assert.Equal($"/api/v1/projects/{project.Id}/{route}/{target.Id}",sentPath);
		Assert.Equal(3,sent.Revision);
		Assert.Equal(target.ParentId,sent.ParentId);
		Assert.Equal(2,sent.SortOrder);
		Assert.Equal("Edited title",sent.Title);
		Assert.Equal(kind=="Decision" ? "Decided" : "Completed",sent.Status);
		if(kind=="Step") Assert.Equal("OwnerApproved",sent.Evidence);
		if(kind!="Decision") { Assert.Equal("Override",sent.AssignmentMode); Assert.Equal(agent,sent.AssignedAgentId); }
		else Assert.Equal("Verified decision",sent.Resolution);
	}
}
