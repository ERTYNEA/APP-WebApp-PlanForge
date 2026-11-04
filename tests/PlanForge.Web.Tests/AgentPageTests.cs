namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Pages;
using System.Net.Http.Json;
using System.Text.Json;

public sealed class AgentPageTests
{
	[Fact]
	public async Task Deactivate_WhenConfirmed_PreservesHistoryAndUsesAgentIdAsync()
	{
		Guid id = Guid.NewGuid();
		string? deleted = null;
		using var context = new ComponentTestContext(request => { if (request.Method == HttpMethod.Delete) deleted = request.RequestUri!.AbsolutePath; return Task.FromResult(ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : new[] { new AgentProfile(id, "NEA", "Planning", true, false) })); });
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "Edit").ClickAsync(new());
		var avatar = page.FindComponent<PlanForge.Web.Components.Shared.AvatarEditor>();
		Assert.Equal($"agents/{id}/avatar", avatar.Instance.ApiPath);
		Assert.NotNull(page.FindComponent<PlanForge.Web.Components.Shared.AgentTokenPanel>());
		await page.FindAll("button").Single(button => button.TextContent == "Deactivate agent").ClickAsync(new());
		Assert.Null(deleted);
		page.Find("#confirm-intent").Change(true);
		await page.Find("#confirm-delete").ClickAsync(new());
		Assert.Equal($"/api/v1/agents/{id}", deleted);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Save_WhenCreatedOrEdited_PersistsLogicalIdentityAndActivityAsync(bool edit)
	{
		Guid id = Guid.NewGuid();
		AgentRequest? sent = null;
		string? method = null;
		using var context = new ComponentTestContext(async request =>
		{
			if (request.Method != HttpMethod.Get) { method = request.Method.Method; sent = await request.Content!.ReadFromJsonAsync<AgentRequest>(); return ComponentTestContext.Json(new AgentProfile(id, sent!.Name, sent.Description, sent.IsActive, false)); }
			return ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : new[] { new AgentProfile(id, "NEA", "Planning", true, false) });
		});
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == (edit ? "Edit" : "+ New agent")).ClickAsync(new());
		page.Find("#agent-name").Change("Iris");
		page.Find("#agent-description").Change("Review partner");
		if (edit) page.Find("#agent-active").Change(false);
		await page.Find("#agent-form").SubmitAsync();
		Assert.Equal(edit ? "PUT" : "POST", method);
		Assert.Equal("Iris", sent!.Name);
		Assert.Equal("Review partner", sent.Description);
		Assert.Equal(!edit, sent.IsActive);
	}
}
