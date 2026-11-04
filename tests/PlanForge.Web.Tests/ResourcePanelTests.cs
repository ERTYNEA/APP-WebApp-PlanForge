namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using System.Text.Json;
using PlanForge.Contracts.Library;
using PlanForge.Web.Components.Shared;

public sealed class ResourcePanelTests
{
	[Theory]
	[InlineData("global")]
	[InlineData("project")]
	[InlineData("phase")]
	public async Task Upload_WhenScoped_PostsExactBytesWithIndependentTargetHeadersAsync(string scope)
	{
		using var fixture=new ResourcePanelFixture();
		Guid project=Guid.NewGuid(),phase=Guid.NewGuid();
		var page=fixture.Context.Render<ResourcePanel>(parameters=> { if(scope!="global") parameters.Add(component=>component.ProjectId,project); if(scope=="phase") parameters.Add(component=>component.PhaseId,phase); });
		await page.FindAll("button").Single(button=>button.TextContent=="Upload file").ClickAsync(new());
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[]{1,2,3},"españa.txt",contentType:"text/plain"));
		await page.Find("#upload-resource-form").SubmitAsync();
		Assert.Equal(new byte[]{1,2,3},fixture.Body);
		Assert.Equal("POST",Assert.Single(fixture.Commands).Method);
		Assert.Equal("españa.txt",Uri.UnescapeDataString(fixture.Headers["X-File-Name"]));
		Assert.Equal(scope=="project",fixture.Headers.ContainsKey("X-Project-Id"));
		Assert.Equal(scope=="phase",fixture.Headers.ContainsKey("X-Phase-Id"));
		Assert.Empty(page.FindAll("dialog"));
	}

	[Fact]
	public void List_WhenLoaded_DistinguishesAssignedAndOrphanWithoutReadingBinaries()
	{
		Guid projectId=Guid.NewGuid();
		ResourceProfile[] resources=[new(Guid.NewGuid(),"Design","design.png","image/png",123,"hash",2,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,[new(projectId,"Project")],[]),new(Guid.NewGuid(),"Orphan notes","notes.md","text/markdown",40,"hash",1,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,[],[])];
		using var context=new ComponentTestContext(request=>Task.FromResult(ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/usage",StringComparison.Ordinal) ? (object)new ResourceUsage(163,1024,512,false) : new ResourcePage(resources,2,0,50))));
		var page=context.Render<ResourcePanel>();
		page.WaitForAssertion(()=>Assert.Contains("Orphan notes",page.Markup));
		Assert.Contains("Assigned",page.Markup);
		Assert.Contains("Orphan",page.Markup);
		Assert.Contains("design.png",page.Markup);
		Assert.Contains("Direct project: Project",page.Markup);
		Assert.Contains($"/files/resources/{resources[0].Id}",page.Markup);
		Assert.Contains("Clean orphan resources",page.Markup);
		Assert.Empty(page.FindAll("[role=alert]"));
	}
}
