namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Agents;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.State;

public sealed class ExportReceiptReplayTests
{
	[Theory]
	[InlineData("csv", "Export CSV")]
	[InlineData("projects", "Export project")]
	public async Task LocalExport_WhenSamePersistedEventIsObserved_DoesNotNotifyTwiceAsync(string kind, string buttonText)
	{
		var project = new ProjectSnapshot(Guid.NewGuid(), 5, "Audit plan", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, []);
		bool exported = false;
		var change = new ProjectChangeInfo(2, 5, Guid.NewGuid(), "Human", "test", "Project exported successfully", DateTimeOffset.UtcNow) { Operation = ProjectChangeOperation.Export };
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			return Task.FromResult(ComponentTestContext.Json(path.EndsWith("/agents", StringComparison.Ordinal) ? (object)Array.Empty<AgentProfile>()
				: path.EndsWith("/resources", StringComparison.Ordinal) ? Array.Empty<ResourceProfile>()
				: path.EndsWith("/changes", StringComparison.Ordinal) ? new ProjectChangePage(5, exported ? 2 : 1, false, exported ? [change] : []) : project));
		});
		context.JSInterop.Setup<DownloadResult>("planForge.download", $"/files/{kind}/{project.Id}").SetResult(System.Text.Json.JsonSerializer.Deserialize<DownloadResult>("{\"success\":true,\"status\":200,\"changeId\":\"2\"}", new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!);
		var page = context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, project.Id));
		await page.FindAll("button").Single(button => button.TextContent == buttonText).ClickAsync(new());
		exported = true;
		await page.FindAll("button").Single(button => button.TextContent.Contains("Reload", StringComparison.Ordinal)).ClickAsync(new());
		Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
	}
}
