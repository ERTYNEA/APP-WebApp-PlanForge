namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Pages;
using System.Net.Http.Json;
using System.Text.Json;

public sealed class AgentPageTests
{
	[Fact]
	public async Task Delete_WhenConfirmed_NamesAgentAndSendsExplicitPermanentDeletionIntentAsync()
	{
		Guid id = Guid.NewGuid();
		string? deleted = null;
		using var context = new ComponentTestContext(request => { if (request.Method == HttpMethod.Delete) deleted = request.RequestUri!.AbsolutePath; return Task.FromResult(ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : new[] { new AgentProfile(id, "NEA", "Planning", true, false) })); });
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "Edit").ClickAsync(new());
		var avatar = page.FindComponent<PlanForge.Web.Components.Shared.AvatarEditor>();
		Assert.Equal($"agents/{id}/avatar", avatar.Instance.ApiPath);
		Assert.NotNull(page.FindComponent<PlanForge.Web.Components.Shared.AgentTokenPanel>());
		await page.FindAll("button").Single(button => button.TextContent == "Delete agent").ClickAsync(new());
		Assert.Null(deleted);
		Assert.Contains("Delete agent?", page.Markup);
		Assert.Contains("NEA", page.Find("dialog").TextContent);
		Assert.Contains("history", page.Find("dialog").TextContent);
		Assert.Contains("irreversible", page.Find("dialog").TextContent);
		page.Find("#confirm-intent").Change(true);
		await page.Find("#confirm-delete").ClickAsync(new());
		Assert.Equal($"/api/v1/agents/{id}", deleted);
	}

	[Fact]
	public async Task NewAgent_WhenOptionalAvatarSelected_CreatesThenUploadsThroughSharedEditorAsync()
	{
		Guid id = Guid.NewGuid();
		var calls = new List<string>();
		byte[] image = [1, 2, 3];
		byte[]? uploaded = null;
		bool created = false;
		using var context = new ComponentTestContext(async request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method == HttpMethod.Post) { calls.Add("create"); created = true; return ComponentTestContext.Json(new AgentProfile(id, "Test avatar", "", true, false)); }
			if (request.Method == HttpMethod.Put) { calls.Add(path); uploaded = await request.Content!.ReadAsByteArrayAsync(); return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent); }
			return ComponentTestContext.Json(path.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : created ? new[] { new AgentProfile(id, "Test avatar", "", true, uploaded is not null) } : Array.Empty<AgentProfile>());
		});
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "+ New agent").ClickAsync(new());
		page.Find("#agent-name").Change("Test avatar");
		page.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromBinary(image, "avatar.png", contentType: "image/png"));
		Assert.Empty(calls);

		await page.Find("#agent-form").SubmitAsync();

		Assert.Equal(new[] { "create", $"/api/v1/agents/{id}/avatar" }, calls);
		Assert.Equal(image, uploaded);
		Assert.Empty(page.FindAll("dialog"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NewAgent_WhenAvatarUploadFails_ReportsSavedRecordAndRetriesWithoutDuplicateAsync(bool networkFailure)
	{
		Guid id = Guid.NewGuid();
		int created = 0;
		bool fail = true, uploaded = false;
		using var context = new ComponentTestContext(request =>
		{
			if (request.Method == HttpMethod.Post) { created++; return Task.FromResult(ComponentTestContext.Json(new AgentProfile(id, "Partial agent", "", true, false))); }
			if (request.Method == HttpMethod.Put)
			{
				if (fail && networkFailure) throw new HttpRequestException("Controlled test failure");
				if (fail) return Task.FromResult(ComponentTestContext.Json(new { detail = "Avatar rejected." }, System.Net.HttpStatusCode.BadRequest));
				uploaded = true; return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
			}
			return Task.FromResult(ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : created > 0 ? new[] { new AgentProfile(id, "Partial agent", "", true, uploaded) } : Array.Empty<AgentProfile>()));
		});
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "+ New agent").ClickAsync(new());
		page.Find("#agent-name").Change("Partial agent");
		page.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[] { 1, 2, 3 }, "avatar.png", contentType: "image/png"));

		await page.Find("#agent-form").SubmitAsync();

		Assert.Equal(1, created);
		Assert.Contains("The agent was saved, but its avatar upload was not confirmed.", page.Markup);
		Assert.DoesNotContain("Agent saved", string.Join(" ", page.FindAll(".toast").Select(element => element.TextContent)));
		Assert.Contains("Edit agent", page.Find("dialog").TextContent);
		Assert.Equal("Partial agent", Assert.Single(page.FindAll(".agent-card h2")).TextContent);
		fail = false;
		await page.FindAll("button").Single(button => button.TextContent == "Upload avatar").ClickAsync(new());
		Assert.True(uploaded);
		Assert.Equal(1, created);
		Assert.DoesNotContain("The agent was saved, but its avatar upload was not confirmed.", page.Markup);
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
		Assert.Equal("agent-active-help", page.Find("#agent-active").GetAttribute("aria-describedby"));
		Assert.Contains("Reactivating requires a new token", page.Find("#agent-active-help").TextContent);
		Assert.DoesNotContain(page.FindAll("button"), button => button.TextContent.Contains("Deactivate", StringComparison.Ordinal));
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
