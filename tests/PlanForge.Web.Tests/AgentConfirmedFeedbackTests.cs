namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.State;

public sealed class AgentConfirmedFeedbackTests
{
	[Fact]
	public async Task ConfirmedAgentDeletion_WhenRefreshFails_RemovesCardAndKeepsDestructiveResultAsync()
	{
		Guid id = Guid.NewGuid();
		bool deleted = false;
		using var context = new ComponentTestContext(request =>
		{
			if (request.Method == HttpMethod.Delete)
			{
				deleted = true;
				return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
			}
			if (deleted)
				throw new HttpRequestException("Controlled post-delete read failure");
			return Task.FromResult(ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : new[] { new AgentProfile(id, "Owned QA", "", true, false) }));
		});
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "Edit").ClickAsync(new());
		await page.FindAll("button").Single(button => button.TextContent == "Delete agent").ClickAsync(new());
		page.Find("#confirm-intent").Change(true);
		await page.Find("#confirm-delete").ClickAsync(new());
		Assert.True(deleted);
		Assert.Empty(page.FindAll("dialog"));
		Assert.Empty(page.FindAll(".agent-card"));
		Assert.Contains(context.Services.GetRequiredService<NotificationState>().Items, notification => notification.Kind == NotificationKind.Danger && notification.Message == "Agent deleted");
		Assert.DoesNotContain("Your changes have not been confirmed", page.Markup);
	}
}
