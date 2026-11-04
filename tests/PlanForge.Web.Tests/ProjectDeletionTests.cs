namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Pages;
using System.Text.Json;

public sealed class ProjectDeletionTests
{
	[Theory]
	[InlineData("item")]
	[InlineData("project")]
	[InlineData("completed")]
	public async Task Delete_WhenConfirmed_UsesCurrentRevisionAndNeverDeletesOnPreviewAsync(string operation)
	{
		using var fixture=new ProjectPageFixture();
		var page=fixture.Context.Render<ProjectDetail>(parameters=>parameters.Add(component=>component.Id,fixture.Project.Id));
		if(operation!="item") await page.FindAll("button").Single(button=>button.TextContent=="Settings").ClickAsync(new());
		string text=operation=="item" ? "Delete step" : operation=="project" ? "Delete project" : "Clean completed phases";
		await page.FindAll("button").Single(button=>button.TextContent.Contains(text,StringComparison.Ordinal)).ClickAsync(new());
		Assert.Empty(fixture.Commands);
		Assert.Contains("irreversible",page.Markup,StringComparison.OrdinalIgnoreCase);
		Assert.True(page.Find("#confirm-delete").HasAttribute("disabled"));
		page.Find("#confirm-intent").Change(true);
		await page.Find("#confirm-delete").ClickAsync(new());
		var command=Assert.Single(fixture.Commands);
		var body=JsonSerializer.Deserialize<ConfirmedRevisionRequest>(command.Body!,new JsonSerializerOptions(JsonSerializerDefaults.Web));
		Assert.True(body!.Confirmed);
		if(operation=="project") Assert.Equal(0,fixture.ReadsAfterProjectDelete);
		Assert.Equal(5,body.Revision);
		Assert.Equal(operation=="completed" ? "POST" : "DELETE",command.Method);
		Assert.EndsWith(operation=="item" ? $"/steps/{fixture.Project.Items.Single(item=>item.Kind=="Step").Id}" : operation=="project" ? $"/projects/{fixture.Project.Id}" : "/completed-phases/clean",command.Path);
	}

	[Fact]
	public async Task Save_WhenStale_ShowsConflictKeepsEditorAndDoesNotRetryAsync()
	{
		using var fixture=new ProjectPageFixture {Conflict=true};
		var page=fixture.Context.Render<ProjectDetail>(parameters=>parameters.Add(component=>component.Id,fixture.Project.Id));
		await page.FindAll("button").Single(button=>button.TextContent.Contains("Add phase",StringComparison.Ordinal)).ClickAsync(new());
		page.Find("#item-title").Change("Stale phase");
		await page.Find("dialog form").SubmitAsync();
		Assert.Single(fixture.Commands);
		Assert.Contains("Reload and reconcile",page.Markup);
		Assert.Contains("Revision 5",page.Markup);
		Assert.NotEmpty(page.FindAll("dialog"));
	}
}
