namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Web.Components.Layout;

public sealed class NavigationTests
{
	[Theory]
	[InlineData("/", "Home")]
	[InlineData("/projects", "Projects")]
	[InlineData("/library", "Library")]
	[InlineData("/agents", "Agents")]
	public void Breadcrumb_WhenWorkspaceRouteChanges_ShowsRealCurrentLocation(string path, string label)
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(ComponentTestContext.Json(new { })));
		context.Services.GetRequiredService<NavigationManager>().NavigateTo(path);
		var page = context.Render<PlanForge.Web.Components.Shared.Breadcrumbs>();
		Assert.Equal(label, page.Find("[aria-current='page']").TextContent);
		Assert.Equal("/", page.Find("a").GetAttribute("href"));
	}

	[Fact]
	public async Task Breadcrumb_WhenProjectLoads_UsesActualNameAndRetainsProjectSettingsAsync()
	{
		using var fixture = new ProjectPageFixture();
		fixture.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{fixture.Project.Id}");
		var crumbs = fixture.Context.Render<PlanForge.Web.Components.Shared.Breadcrumbs>();
		var detail = fixture.Context.Render<PlanForge.Web.Components.Pages.ProjectDetail>(parameters => parameters.Add(component => component.Id, fixture.Project.Id));
		Assert.Equal(fixture.Project.Name, crumbs.Find("[aria-current='page']").TextContent);
		Assert.Equal("/projects", crumbs.FindAll("a").Single(link => link.TextContent == "Projects").GetAttribute("href"));

		await detail.FindAll("button").Single(button => button.TextContent == "Settings").ClickAsync(new());

		Assert.Equal("Settings", crumbs.Find("[aria-current='page']").TextContent);
		Assert.Equal($"/projects/{fixture.Project.Id}", crumbs.FindAll("a").Single(link => link.TextContent == fixture.Project.Name).GetAttribute("href"));
		Assert.Contains("Project settings", detail.Find("dialog").TextContent);
	}

	[Fact]
	public void AccountRoute_WhenOpen_UsesOnlyBottomProfileAndContextualBreadcrumb()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(ComponentTestContext.Json(new { })));
		context.AddAuthorization().SetAuthorized("test");
		context.Services.GetRequiredService<NavigationManager>().NavigateTo("/settings");
		var page = context.Render<MainLayout>();

		Assert.Single(page.FindAll("a[href='/settings']"));
		Assert.Contains("active", page.Find(".account-link").ClassList);
		Assert.Equal("page", page.Find(".account-link").GetAttribute("aria-current"));
		Assert.Empty(page.FindAll(".sidebar nav a[href='/settings']"));
		Assert.Empty(page.FindAll(".topbar a[href='/settings']"));
		Assert.Contains("Account", page.Find(".breadcrumb").TextContent);
		Assert.Contains("Settings", page.Find(".breadcrumb [aria-current='page']").TextContent);
	}
}
