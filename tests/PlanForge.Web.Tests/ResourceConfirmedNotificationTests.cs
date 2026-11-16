namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Library;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public sealed class ResourceConfirmedNotificationTests
{
	[Fact]
	public async Task Unlink_WhenCommittedButRefreshFails_RetainsDestructiveOutcomeAndTruthfulWarningAsync()
	{
		Guid projectId = Guid.NewGuid();
		var resource = new ResourceProfile(Guid.NewGuid(), "Audit file", "audit.txt", "text/plain", 3, "hash", 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [new(projectId, "Audit project")], []);
		bool committed = false;
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method == HttpMethod.Post)
			{
				committed = true;
				resource = resource with { Revision = 3, Projects = [] };
				return Task.FromResult(ComponentTestContext.Json(resource));
			}
			if (committed)
				throw new HttpRequestException("Controlled read failure after successful mutation");
			object value = path.EndsWith("/usage", StringComparison.Ordinal) ? new ResourceUsage(3, 10000, 1024, false)
				: path.EndsWith("/summary", StringComparison.Ordinal) ? new PlanForge.Contracts.Planning.ProjectSummaryPage([], 0, 0, 100, false)
				: new ResourcePage([resource], 1, 0, 50);
			return Task.FromResult(ComponentTestContext.Json(value));
		});
		var panel = context.Render<ResourcePanel>();
		await panel.FindAll("button").Single(button => button.TextContent == "Manage").ClickAsync(new());
		await panel.Find("button[aria-label='Unlink association']").ClickAsync(new());
		Assert.True(committed);
		ToastNotification toast = Assert.Single(context.Services.GetRequiredService<NotificationState>().Items);
		Assert.Equal(NotificationKind.Danger, toast.Kind);
		Assert.Equal("Resource unlinked", toast.Message);
		Assert.Contains("Your changes were confirmed, but the view could not be refreshed", panel.Markup);
		Assert.DoesNotContain("Your changes have not been confirmed", panel.Markup);
	}
}
