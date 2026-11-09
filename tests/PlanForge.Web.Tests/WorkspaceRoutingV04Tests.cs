namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components;

public sealed class WorkspaceRoutingV04Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DirectHomeProjectsNavigation_RendersImmediatelyAndCannotLoseDestinationWhileBusyAsync(bool holdProjectRequest)
	{
		var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		bool projectRequestStarted = false;
		int dashboardRequests = 0;
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.PathAndQuery;
			if (path.Contains("/dashboard", StringComparison.Ordinal))
			{
				dashboardRequests++;
				return Task.FromResult(ComponentTestContext.Json(new WorkspaceDashboard(0, 0, 0, 0, [])));
			}
			if (path.Contains("take=100", StringComparison.Ordinal))
			{
				projectRequestStarted = true;
				if (holdProjectRequest)
					return pending.Task;
			}
			return Task.FromResult(ComponentTestContext.Json(new ProjectSummaryPage([], 0, 0, 25, false)));
		});
		var authorization = context.AddAuthorization();
		authorization.SetAuthorized("test");
		authorization.SetClaims(new System.Security.Claims.Claim(PlanForge.Web.Security.WebSession.TokenClaim, "test-session"));
		context.SetRendererInfo(new RendererInfo("Server", true));
		var navigation = context.Services.GetRequiredService<NavigationManager>();
		navigation.NavigateTo("/");
		var page = context.Render<Routes>();
		Assert.True(page.FindAll("#workspace-metrics").Count > 0, "Initial router harness markup: " + page.Markup);
		page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("#workspace-metrics")));
		try
		{
			await page.InvokeAsync(() => navigation.NavigateTo("/projects"));
			page.WaitForAssertion(() => Assert.Equal("Projects.", page.Find("h1").TextContent), TimeSpan.FromSeconds(1));
			page.WaitForAssertion(() => Assert.True(projectRequestStarted), TimeSpan.FromSeconds(1));
			Assert.Empty(page.FindAll("#workspace-metrics, #next-in-focus"));
			await page.InvokeAsync(() => navigation.NavigateTo("/"));
			page.WaitForAssertion(() => Assert.Equal("Work in focus.", page.Find("h1").TextContent), TimeSpan.FromSeconds(1));
			page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("#workspace-metrics")), TimeSpan.FromSeconds(1));
			Assert.Equal(2, dashboardRequests);
		}
		finally
		{
			pending.TrySetResult(ComponentTestContext.Json(new ProjectSummaryPage([], 0, 0, 100, false)));
		}
		await page.InvokeAsync(() => Task.CompletedTask);
		Assert.Equal("Work in focus.", page.Find("h1").TextContent);
		Assert.Empty(page.FindAll(".filter-bar, .project-grid"));
	}
}
