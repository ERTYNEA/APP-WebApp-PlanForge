namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Library;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public sealed class ConfirmedWriteFeedbackTests
{
	[Theory]
	[InlineData("title", "Resource updated")]
	[InlineData("upload", "Resource uploaded successfully")]
	[InlineData("attach", "Resource attached")]
	public async Task ResourceWrite_WhenRefreshFails_PreservesConfirmedOutcomeAsync(string operation, string message)
	{
		var resource = new ResourceProfile(Guid.NewGuid(), "Fixture", "fixture.txt", "text/plain", 3, "hash", 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []);
		bool committed = false;
		using var context = new ComponentTestContext(request =>
		{
			if (request.Method != HttpMethod.Get)
			{
				committed = true;
				return Task.FromResult(ComponentTestContext.Json(resource));
			}
			if (committed)
				throw new HttpRequestException("Controlled follow-up read failure");
			return Task.FromResult(ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/usage", StringComparison.Ordinal)
				? (object)new ResourceUsage(3, 10000, 1024, false)
				: request.RequestUri.AbsolutePath.EndsWith("/summary", StringComparison.Ordinal)
					? new PlanForge.Contracts.Planning.ProjectSummaryPage([], 0, 0, 100, false) : request.RequestUri.AbsolutePath.Contains("/projects/", StringComparison.Ordinal) && operation == "attach" ? Array.Empty<ResourceProfile>() : new ResourcePage([resource], 1, 0, 50)));
		});
		var panel = context.Render<ResourcePanel>(parameters => parameters.Add(component => component.ProjectId, operation == "attach" ? Guid.NewGuid() : null));
		if (operation == "title")
		{
			await ButtonAsync(panel, "Manage");
			await panel.Find("#resource-metadata-form").SubmitAsync();
		}
		else if (operation == "upload")
		{
			await ButtonAsync(panel, "Upload file");
			panel.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("abc", "fixture.txt"));
			await panel.Find("#upload-resource-form").SubmitAsync();
		}
		else
		{
			await ButtonAsync(panel, "Attach existing");
			panel.Find("dialog select").Change(resource.Id.ToString());
			await panel.Find("#attach-resource-form").SubmitAsync();
		}
		Assert.True(committed);
		IReadOnlyList<ToastNotification> notifications = context.Services.GetRequiredService<NotificationState>().Items;
		Assert.Contains(notifications, toast => toast.Kind == NotificationKind.Success && toast.Message == message);
		Assert.Contains("Your changes were confirmed, but the view could not be refreshed", panel.Markup + string.Join(" ", notifications.Select(toast => toast.Message)));
		Assert.DoesNotContain("Your changes have not been confirmed", panel.Markup + string.Join(" ", notifications.Select(toast => toast.Message)));
	}

	private static Task ButtonAsync(IRenderedComponent<ResourcePanel> panel, string text) => panel.FindAll("button").Single(button => button.TextContent == text).ClickAsync(new());
}
