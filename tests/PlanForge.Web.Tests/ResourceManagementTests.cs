namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Library;
using PlanForge.Web.Components.Shared;
using System.Text.Json;

public sealed class ResourceManagementTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Delete_WhenPreviewConfirmed_UsesOnlyPreviewedIdsAndExplicitIntentAsync(bool cleanup)
	{
		using var fixture=new ResourcePanelFixture();
		var page=fixture.Context.Render<ResourcePanel>();
		if(!cleanup) await page.FindAll("button").Single(button=>button.TextContent=="Manage").ClickAsync(new());
		await page.FindAll("button").Single(button=>button.TextContent==(cleanup ? "Clean orphan resources" : "Delete resource")).ClickAsync(new());
		Assert.Empty(fixture.Commands);
		Assert.Contains("test.txt",page.Markup);
		Assert.True(page.Find("#confirm-delete").HasAttribute("disabled"));
		page.Find("#confirm-intent").Change(true);
		await page.Find("#confirm-delete").ClickAsync(new());
		var command=Assert.Single(fixture.Commands);
		using var json=JsonDocument.Parse(fixture.Body!);
		Assert.True(json.RootElement.GetProperty("confirmed").GetBoolean());
		if(cleanup) Assert.Equal(fixture.Resource.Id,json.RootElement.GetProperty("resourceIds")[0].GetGuid());
		else Assert.Equal(2,json.RootElement.GetProperty("revision").GetInt64());
		Assert.Equal(cleanup ? "POST" : "DELETE",command.Method);
		Assert.Empty(page.FindAll("dialog"));
	}

	[Theory]
	[InlineData(false,false)]
	[InlineData(true,false)]
	[InlineData(false,true)]
	[InlineData(true,true)]
	public async Task ManageAssociation_WhenChanged_PreservesIndependentTargetAndResourceRevisionAsync(bool phaseOnly,bool detach)
	{
		using var fixture=new ResourcePanelFixture();
		if(detach) fixture.Resource=fixture.Resource with {Projects=phaseOnly ? [] : [new(fixture.ProjectId,"Target project")],Phases=phaseOnly ? [new(fixture.PhaseId,"Target phase",fixture.ProjectId,"Target project")] : []};
		var page=fixture.Context.Render<ResourcePanel>();
		await page.FindAll("button").Single(button=>button.TextContent=="Manage").ClickAsync(new());
		if(detach) await page.Find("button[aria-label='Unlink association']").ClickAsync(new());
		else
		{
			page.Find("#link-project").Change(fixture.ProjectId.ToString());
			if(phaseOnly) page.Find("#link-phase").Change(fixture.PhaseId.ToString());
			await page.Find("#resource-link-form").SubmitAsync();
		}
		Assert.EndsWith("/links",Assert.Single(fixture.Commands).Path);
		var link=JsonSerializer.Deserialize<ResourceLinkRequest>(fixture.Body!,new JsonSerializerOptions(JsonSerializerDefaults.Web));
		Assert.Equal(!detach,link!.Attach);
		Assert.Equal(phaseOnly ? null : fixture.ProjectId,link.ProjectId);
		Assert.Equal(phaseOnly ? fixture.PhaseId : null,link.PhaseId);
		Assert.Equal(2,link.Revision);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AttachExisting_WhenContextChosen_UsesOnlyIndependentProjectOrPhaseLinkAsync(bool phaseOnly)
	{
		using var fixture=new ResourcePanelFixture();
		Guid project=Guid.NewGuid(),phase=Guid.NewGuid();
		var page=fixture.Context.Render<ResourcePanel>(parameters=> { parameters.Add(component=>component.ProjectId,project); if(phaseOnly) parameters.Add(component=>component.PhaseId,phase); });
		await page.FindAll("button").Single(button=>button.TextContent=="Attach existing").ClickAsync(new());
		page.Find("#existing-resource").Change(fixture.Resource.Id.ToString());
		await page.Find("#attach-resource-form").SubmitAsync();
		Assert.EndsWith($"/resources/{fixture.Resource.Id}/links",Assert.Single(fixture.Commands).Path);
		var link=JsonSerializer.Deserialize<ResourceLinkRequest>(fixture.Body!,new JsonSerializerOptions(JsonSerializerDefaults.Web));
		Assert.True(link!.Attach);
		Assert.Equal(2,link.Revision);
		Assert.Equal(phaseOnly ? phase : null,link.PhaseId);
		Assert.Equal(phaseOnly ? null : project,link.ProjectId);
	}

	[Fact]
	public async Task Rename_WhenSaved_UsesResourceRevisionNotProjectRevisionAsync()
	{
		using var fixture=new ResourcePanelFixture();
		var page=fixture.Context.Render<ResourcePanel>();
		await page.FindAll("button").Single(button=>button.TextContent=="Manage").ClickAsync(new());
		page.Find("#resource-title").Change("Renamed file");
		await page.Find("#resource-metadata-form").SubmitAsync();
		var command=Assert.Single(fixture.Commands);
		Assert.Equal("PUT",command.Method);
		var metadata=JsonSerializer.Deserialize<ResourceMetadataRequest>(fixture.Body!,new JsonSerializerOptions(JsonSerializerDefaults.Web));
		Assert.Equal(2,metadata!.Revision);
		Assert.Equal("Renamed file",metadata.Title);
	}
}
