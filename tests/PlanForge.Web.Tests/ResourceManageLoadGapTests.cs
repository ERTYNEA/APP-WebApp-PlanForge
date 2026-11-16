namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;
using PlanForge.Web;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;
using System.Net;

public sealed class ResourceManageLoadGapTests
{
	[Theory]
	[InlineData("")]
	[InlineData("title")]
	[InlineData("unlink")]
	public async Task Manage_PrerequisiteFailureHasOneDialogOwnerAndClearsStaleErrorsAsync(string previousOperation)
	{
		Guid projectId = Guid.NewGuid();
		var resource = new ResourceProfile(Guid.NewGuid(), "File", "file.txt", "text/plain", 3, "hash", 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [new(projectId, "Project")], []);
		bool failLoad = previousOperation.Length == 0;
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method != HttpMethod.Get)
				return Task.FromResult(ComponentTestContext.Json(new { detail = "Previous mutation failed." }, HttpStatusCode.BadRequest));
			if (path.EndsWith("/summary", StringComparison.Ordinal) && failLoad)
				throw new HttpRequestException("Manage prerequisites unavailable.");
			object value = path.EndsWith("/usage", StringComparison.Ordinal) ? new ResourceUsage(3, 10000, 1024, false)
				: path.EndsWith("/summary", StringComparison.Ordinal) ? new ProjectSummaryPage([], 0, 0, 100, false)
				: new ResourcePage([resource], 1, 0, 50);
			return Task.FromResult(ComponentTestContext.Json(value));
		});
		var page = context.Render<ResourcePanel>();
		await page.FindAll("button").Single(button => button.TextContent == "Manage").ClickAsync(new());
		if (previousOperation.Length > 0)
		{
			if (previousOperation == "title")
				await page.Find("#resource-metadata-form").SubmitAsync();
			else
				await page.FindAll("button").Single(button => button.TextContent == "Unlink").ClickAsync(new());
			Assert.Contains("Previous mutation failed.", Assert.Single(page.FindAll("[role=alert]")).TextContent);
			await page.Find("button[aria-label='Close dialog']").ClickAsync(new());
			Assert.Empty(page.FindAll("[role=alert]"));
			failLoad = true;
			await page.FindAll("button").Single(button => button.TextContent == "Manage").ClickAsync(new());
		}

		var error = Assert.Single(page.FindAll("[role=alert]"));
		Assert.Contains(UiText.Get("The service is temporarily unavailable. Your changes have not been confirmed. Reload before retrying."), error.TextContent);
		Assert.NotNull(error.Closest("dialog"));
		Assert.Null(error.Closest("form, .resource-associations"));
		Assert.DoesNotContain("Previous mutation failed.", page.Markup);
		Assert.Empty(context.Services.GetRequiredService<NotificationState>().Items);

		await page.Find("button[aria-label='Close dialog']").ClickAsync(new());
		Assert.Empty(page.FindAll("[role=alert]"));
		failLoad = false;
		await page.FindAll("button").Single(button => button.TextContent == "Manage").ClickAsync(new());
		Assert.Empty(page.FindAll("[role=alert]"));
	}
}
