namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using PlanForge.Web.Components.Pages;

public sealed class FileSelectionV07Tests
{
	[Fact]
	public async Task Resource_ReselectionClearsUploadFailureAndReopenDropsOldFileAndTitleAsync()
	{
		using var fixture = new ResourcePanelFixture { FailMutation = true };
		var page = fixture.Context.Render<PlanForge.Web.Components.Shared.ResourcePanel>();
		await page.FindAll("button").Single(button => button.TextContent == "Upload file").ClickAsync(new());
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("bad", "bad.txt"));
		await page.Find("#upload-resource-form").SubmitAsync();
		Assert.Contains("Resource fixture failure.", page.Find("dialog").TextContent);
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("new", "new.txt"));
		Assert.DoesNotContain("Resource fixture failure.", page.Find("dialog").TextContent);
		Assert.Equal("new.txt", page.Find("#upload-resource-form input:not([type=file])").GetAttribute("value"));
		await page.FindAll("dialog button").Single(button => button.TextContent == "Cancel").ClickAsync(new());
		await page.FindAll("button").Single(button => button.TextContent == "Upload file").ClickAsync(new());
		Assert.Equal("", page.Find("#upload-resource-form input:not([type=file])").GetAttribute("value"));
		Assert.True(page.Find("#upload-resource-form button.primary").HasAttribute("disabled"));
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("retry", "retry.txt"));
		fixture.FailMutation = false;
		await page.Find("#upload-resource-form").SubmitAsync();
		Assert.Single(fixture.Commands);
		Assert.Empty(page.FindAll("dialog"));
	}

	[Fact]
	public async Task Csv_ReselectionClearsOnlyAttemptFeedbackAndReopenResetsSelectionAsync()
	{
		using var fixture = new ProjectPageFixture { Conflict = true };
		var page = fixture.Context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, fixture.Project.Id));
		await page.FindAll("button").Single(button => button.TextContent == "Import CSV").ClickAsync(new());
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("bad", "bad.csv"));
		page.Find("#replace-plan").Change(true);
		await page.Find("#import-plan-form").SubmitAsync();
		Assert.Contains("The project changed.", page.Find("dialog").TextContent);
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("new", "new.csv"));
		Assert.DoesNotContain("The project changed.", page.Find("dialog").TextContent);
		Assert.DoesNotContain("Reload and reconcile", page.Find("dialog").TextContent);
		Assert.Single(fixture.Commands);
		await page.FindAll("dialog button").Single(button => button.TextContent == "Cancel").ClickAsync(new());
		await page.FindAll("button").Single(button => button.TextContent == "Import CSV").ClickAsync(new());
		Assert.False(page.Find("#replace-plan").HasAttribute("checked"));
		Assert.True(page.Find("#import-plan-form button.primary").HasAttribute("disabled"));
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("retry", "retry.csv"));
		fixture.Conflict = false;
		await page.Find("#import-plan-form").SubmitAsync();
		Assert.Equal("retry", fixture.Commands.Last().Body);
		Assert.Empty(page.FindAll("dialog"));
	}
}
