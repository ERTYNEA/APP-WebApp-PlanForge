namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using PlanForge.Web.Components.Pages;
using System.Text;

public sealed class ProjectExchangeTests
{
	[Fact]
	public async Task Refresh_WhenAgentExportKeepsRevision_ObservesNewPersistedEventOnceAsync()
	{
		using var fixture = new ProjectPageFixture();
		var page = fixture.Context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, fixture.Project.Id));
		fixture.Action = "Project exported successfully by agent";
		await page.FindAll("button").Single(button => button.TextContent.Contains("Reload", StringComparison.Ordinal)).ClickAsync(new());
		Assert.Contains("Project exported successfully by agent", page.Markup);
		await page.FindAll("button").Single(button => button.TextContent.Contains("Reload", StringComparison.Ordinal)).ClickAsync(new());
		Assert.Single(page.FindAll(".toast"), element => element.TextContent.Contains("Project exported successfully by agent", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("Export project", "projects", true)]
	[InlineData("Export CSV", "csv", true)]
	[InlineData("Export project", "projects", false)]
	public async Task Export_WhenDownloadCompletes_ShowsSuccessOnlyAfterVerifiedHttpAsync(string buttonText, string kind, bool success)
	{
		using var fixture = new ProjectPageFixture();
		var download = fixture.Context.JSInterop.Setup<PlanForge.Web.DownloadResult>("planForge.download", $"/files/{kind}/{fixture.Project.Id}");
		download.SetResult(new(success, success ? 200 : 409));
		var page = fixture.Context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, fixture.Project.Id));
		await page.FindAll("button").Single(button => button.TextContent == buttonText).ClickAsync(new());
		Assert.Single(download.Invocations);
		Assert.Equal(success, page.Markup.Contains("Project exported successfully", StringComparison.Ordinal));
		if (!success) Assert.Contains("Reload and reconcile", page.Markup);
	}

	[Fact]
	public async Task Import_WhenExplicitReplacementSelected_PostsExactUtf8AndConfirmationAsync()
	{
		using var fixture = new ProjectPageFixture();
		var page = fixture.Context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, fixture.Project.Id));
		await page.FindAll("button").Single(button => button.TextContent == "Import CSV").ClickAsync(new());
		string csv = "SchemaVersion;ProjectId;Title\n1;" + fixture.Project.Id + ";\"España; ñ\"";
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(csv, "work-plan.csv"));
		page.Find("#replace-plan").Change(true);
		await page.Find("#import-plan-form").SubmitAsync();
		var sent = Assert.Single(fixture.Commands);
		Assert.Equal("POST", sent.Method);
		Assert.EndsWith("/work-plan/import", sent.Path);
		Assert.Equal(csv, sent.Body);
		Assert.Contains("CSV imported successfully", page.Markup);
		Assert.Empty(page.FindAll("dialog"));
	}
}
