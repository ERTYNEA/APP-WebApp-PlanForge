namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Shared;

public sealed class AgentTokenPanelTests
{
	[Fact]
	public void List_WhenHistoryExists_ShowsValidTypesAndCollapsedPagedHistory()
	{
		Guid agentId = Guid.NewGuid();
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var tokens = new List<AgentTokenProfile> { new(Guid.NewGuid(), "Personal integration", null, now, now.AddDays(1), null, null), new(Guid.NewGuid(), "Linked integration", agentId, now, now.AddDays(1), null, null) };
		tokens.AddRange(Enumerable.Range(0, 13).Select(index => new AgentTokenProfile(Guid.NewGuid(), "Old " + index, null, now.AddDays(-40), now.AddDays(-10), index % 2 == 0 ? now.AddDays(-20) : null, null)));
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(tokens)));
		var page = context.Render<AgentTokenPanel>(parameters => parameters.Add(component => component.Agents, new[] { new AgentProfile(agentId, "Test agent", "", true, false) }));

		Assert.Equal(2, page.FindAll(".valid-token-list .token-row").Count);
		Assert.Contains("Personal token", page.Find(".valid-token-list").TextContent);
		Assert.Contains("Agent token", page.Find(".valid-token-list").TextContent);
		Assert.Equal("Revoked & expired tokens (13)", page.Find("#token-history summary").TextContent.Trim());
		Assert.False(page.Find("#token-history").HasAttribute("open"));
		Assert.Equal(10, page.FindAll("#token-history .token-row").Count);
		Assert.Equal(2, page.FindAll("button").Count(button => button.TextContent == "Renew"));
	}

	[Fact]
	public async Task List_WhenManyValidTokens_RendersBoundedPagesWithoutHidingHistoryAsync()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var tokens = Enumerable.Range(1, 27).Select(index => new AgentTokenProfile(Guid.NewGuid(), "Valid " + index, null, now.AddMinutes(-index), now.AddDays(1), null, null)).ToList();
		tokens.Add(new(Guid.NewGuid(), "Revoked history", null, now, now.AddDays(1), now, null));
		using var context = new ComponentTestContext(request => Task.FromResult(ComponentTestContext.Json(tokens)));
		var page = context.Render<AgentTokenPanel>();
		Assert.Equal(25, page.FindAll(".valid-token-list .token-row").Count);
		Assert.True(page.Find("#valid-token-pages button:first-child").HasAttribute("disabled"));
		await page.Find("#valid-token-pages button:last-child").ClickAsync(new());
		Assert.Equal(2, page.FindAll(".valid-token-list .token-row").Count);
		Assert.Contains("Valid 27", page.Find(".valid-token-list").TextContent);
		Assert.Contains("Revoked & expired tokens (1)", page.Find("#token-history summary").TextContent);
		Assert.True(page.Find("#valid-token-pages button:last-child").HasAttribute("disabled"));
		await page.Find("#valid-token-pages button:first-child").ClickAsync(new());
		Assert.Equal(25, page.FindAll(".valid-token-list .token-row").Count);
	}

	[Fact]
	public async Task Cleanup_WhenExplicitlyConfirmed_DeletesHistoryAndPreservesValidTokensAsync()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var valid = new AgentTokenProfile(Guid.NewGuid(), "Survivor", null, now, now.AddDays(1), null, null);
		var revoked = valid with { Id = Guid.NewGuid(), Name = "Old credential", RevokedAt = now };
		bool cleaned = false;
		string? body = null, path = null;
		using var context = new ComponentTestContext(async request =>
		{
			if (request.Method == HttpMethod.Post) { body = await request.Content!.ReadAsStringAsync(); path = request.RequestUri!.AbsolutePath; cleaned = true; return ComponentTestContext.Json(new { deletedCount = 1 }); }
			return ComponentTestContext.Json(cleaned ? new[] { valid } : new[] { valid, revoked });
		});
		var page = context.Render<AgentTokenPanel>();
		var cleanupButton = page.FindAll("button").Single(button => button.TextContent == "Delete revoked tokens");
		Assert.False(cleanupButton.HasAttribute("disabled"));
		await cleanupButton.ClickAsync(new());
		Assert.False(cleaned);
		Assert.Contains("expired", page.Find("dialog").TextContent);
		page.Find("#confirm-intent").Change(true);

		await page.Find("#confirm-delete").ClickAsync(new());

		Assert.True(cleaned);
		Assert.Equal("/api/v1/agent-tokens/history/clean", path);
		using var payload = System.Text.Json.JsonDocument.Parse(body!);
		Assert.True(payload.RootElement.GetProperty("confirmed").GetBoolean());
		Assert.Contains("Survivor", page.Find(".valid-token-list").TextContent);
		Assert.DoesNotContain("Old credential", page.Markup);
		Assert.Empty(page.FindAll("dialog"));
	}

	[Fact]
	public async Task Revoke_WhenConfirmed_UsesCredentialIdAndRefreshesMetadataAsync()
	{
		Guid id = Guid.NewGuid();
		var credential = new AgentTokenProfile(id, "Automation", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), null, null);
		string? path = null;
		using var context = new ComponentTestContext(request => { if (request.Method == HttpMethod.Delete) { path = request.RequestUri!.AbsolutePath; return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent)); } return Task.FromResult(ComponentTestContext.Json(new[] { credential })); });
		var page = context.Render<AgentTokenPanel>();
		await page.FindAll("button").Single(button => button.TextContent == "Revoke").ClickAsync(new());
		Assert.Null(path);
		page.Find("#confirm-intent").Change(true);
		await page.Find("#confirm-delete").ClickAsync(new());
		Assert.Equal($"/api/v1/agent-tokens/{id}", path);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Issue_WhenCreatedOrRenewed_ShowsNewSecretOnlyUntilDismissedAsync(bool renew)
	{
		Guid id = Guid.NewGuid();
		string secret = "pf_a_" + new string('c', 64);
		var credential = new AgentTokenProfile(id, "Automation", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), null, null);
		string? path = null;
		using var context = new ComponentTestContext(request => { if (request.Method == HttpMethod.Post) path = request.RequestUri!.AbsolutePath; return Task.FromResult(ComponentTestContext.Json(request.Method == HttpMethod.Get ? (object)new[] { credential } : new AgentTokenGrant(secret, credential))); });
		var page = context.Render<AgentTokenPanel>();
		Assert.DoesNotContain(secret, page.Markup);
		await page.FindAll("button").Single(button => button.TextContent == (renew ? "Renew" : "+ Create token")).ClickAsync(new());
		if (!renew) { page.Find("#token-name").Change("Automation"); await page.Find("#token-form").SubmitAsync(); }
		Assert.Equal(renew ? $"/api/v1/agent-tokens/{id}/renew" : "/api/v1/agent-tokens", path);
		Assert.Contains(secret, page.Markup);
		Assert.Contains("shown only once", page.Markup);
		await page.Find("#dismiss-token").ClickAsync(new());
		Assert.DoesNotContain(secret, page.Markup);
	}
}
