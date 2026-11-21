namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Pages;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;
using System.Net;

public sealed class FileSelectionGuardV07Tests
{
	[Theory]
	[InlineData("api", true)]
	[InlineData("http", true)]
	[InlineData("file", true)]
	[InlineData("timeout", true)]
	[InlineData("success", true)]
	[InlineData("api", false)]
	[InlineData("http", false)]
	[InlineData("file", false)]
	[InlineData("timeout", false)]
	public async Task Csv_RouteChangeDuringImportRetiresOldSelectionAndCannotPublishLateResultAsync(string outcome, bool changeRoute)
	{
		Guid first = Guid.NewGuid(), second = Guid.NewGuid();
		int imports = 0;
		var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		var fileRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method == HttpMethod.Post)
			{
				imports++;
				Assert.EndsWith($"/projects/{first}/work-plan/import", path);
				return response.Task;
			}
			object value = path.EndsWith("/agents", StringComparison.Ordinal) ? Array.Empty<AgentProfile>() : path.EndsWith("/resources", StringComparison.Ordinal) ? Array.Empty<PlanForge.Contracts.Library.ResourceProfile>() : path.EndsWith("/changes", StringComparison.Ordinal) ? new PlanForge.Contracts.Planning.ProjectChangePage(1, 0, false, []) : new PlanForge.Contracts.Planning.ProjectSnapshot(path.Contains(second.ToString(), StringComparison.Ordinal) ? second : first, 1, path.Contains(second.ToString(), StringComparison.Ordinal) ? "Second project" : "First project", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, []);
			return Task.FromResult(ComponentTestContext.Json(value));
		});
		var page = context.Render<ProjectDetail>(parameters => parameters.Add(component => component.Id, first));
		await page.FindAll("button").Single(button => button.TextContent == "Import CSV").ClickAsync(new());
		if (outcome == "file")
			await page.InvokeAsync(() => page.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([new HeldCsvFile(fileRead.Task)])));
		else
			page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("first", "first.csv"));
		Task importing = page.Find("#import-plan-form").SubmitAsync();
		try
		{
			page.WaitForAssertion(() => Assert.True(page.Find("input[type=file]").HasAttribute("disabled")));
			if (changeRoute)
				page.Render(parameters => parameters.Add(component => component.Id, second));
		}
		finally
		{
			if (outcome == "file")
				fileRead.SetException(new IOException("Old file failure"));
			else if (outcome == "http")
				response.SetException(new HttpRequestException("Old HTTP failure"));
			else if (outcome == "timeout")
				response.SetException(new OperationCanceledException("Old request timeout"));
			else
				response.SetResult(outcome == "success" ? ComponentTestContext.Json(new PlanForge.Contracts.Planning.ProjectSnapshot(first, 2, "Old imported project", "", "Pending", "PlanningOnly", DateTimeOffset.UtcNow, [])) : ComponentTestContext.Json(new { detail = "Old import failure" }, HttpStatusCode.Conflict));
			await importing;
		}
		Assert.Equal(outcome == "file" ? 0 : 1, imports);
		Assert.Empty(context.Services.GetRequiredService<NotificationState>().Items);
		if (changeRoute)
		{
			Assert.Empty(page.FindAll("dialog"));
			Assert.Contains("Second project", page.Find("h1").TextContent);
			Assert.DoesNotContain("Old import failure", page.Markup);
			Assert.DoesNotContain("Old imported project", page.Markup);
			await page.FindAll("button").Single(button => button.TextContent == "Import CSV").ClickAsync(new());
			Assert.True(page.Find("#import-plan-form button.primary").HasAttribute("disabled"));
		}
		else
		{
			string expected = outcome switch
			{
				"http" => "The service is temporarily unavailable.",
				"file" => "The file could not be read.",
				"timeout" => "The request timed out.",
				_ => "Old import failure"
			};
			Assert.Contains("First project", page.Find("h1").TextContent);
			Assert.Contains(expected, page.Find("dialog").TextContent);
			Assert.False(page.Find("#import-plan-form button.primary").HasAttribute("disabled"));
			Assert.Equal(outcome == "api", page.Find("dialog").TextContent.Contains("Reload and reconcile", StringComparison.Ordinal));
		}
	}

	private sealed class HeldCsvFile(Task read) : IBrowserFile
	{
		public string Name => "first.csv";
		public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
		public long Size => 5;
		public string ContentType => "text/csv";
		public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new HeldCsvStream(read);
	}

	private sealed class HeldCsvStream(Task read) : MemoryStream
	{
		public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => read;
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Avatar_SelectionPreservesRemovalOrConfirmedRefreshErrorAsync(bool refreshError)
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(refreshError ? AvatarTestResponses.Json(new AvatarState(true, Guid.NewGuid())) : ComponentTestContext.Json(new { detail = "Removal failed" }, HttpStatusCode.BadRequest)));
		var page = context.Render<AvatarEditor>(parameters => parameters.Add(component => component.ApiPath, "users/me/avatar").Add(component => component.HasAvatar, true).Add(component => component.Changed, _ => throw new HttpRequestException("Refresh failed")));
		if (refreshError)
		{
			page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("first", "first.png", contentType: "image/png"));
			await page.Find("form").SubmitAsync();
		}
		else
		{
			page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("bad", "bad.png", contentType: "image/png"));
			await page.Find("form").SubmitAsync();
			await page.Find("button[aria-label='Remove avatar']").ClickAsync(new());
		}
		string error = page.Find("[role=alert]").TextContent;
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("next", "next.png", contentType: "image/png"));
		Assert.Equal(error, page.Find("[role=alert]").TextContent);
	}

	[Fact]
	public async Task Agents_SelectionDoesNotClearUnrelatedIdentitySaveFailureAsync()
	{
		using var context = new ComponentTestContext(request => Task.FromResult(request.Method == HttpMethod.Post ? ComponentTestContext.Json(new { detail = "Identity rejected" }, HttpStatusCode.BadRequest) : ComponentTestContext.Json(request.RequestUri!.AbsolutePath.EndsWith("/agent-tokens", StringComparison.Ordinal) ? (object)Array.Empty<AgentTokenProfile>() : Array.Empty<AgentProfile>())));
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "+ New agent").ClickAsync(new());
		page.Find("#agent-name").Change("New");
		await page.Find("#agent-form").SubmitAsync();
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("next", "next.png", contentType: "image/png"));
		Assert.Contains("Identity rejected", page.Find("dialog").TextContent);
		Assert.DoesNotContain("The agent was saved.", page.Markup);
	}

	[Fact]
	public async Task Avatar_BusySelectorRejectsSyntheticChangeAndSnapshotsOriginalFileAsync()
	{
		var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		byte[]? sent = null;
		using var context = new ComponentTestContext(async request => { sent = await request.Content!.ReadAsByteArrayAsync(); return await response.Task; });
		var page = context.Render<AvatarEditor>(parameters => parameters.Add(component => component.ApiPath, "users/me/avatar"));
		page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("first", "first.png", contentType: "image/png"));
		Task submit = page.Find("form").SubmitAsync();
		try
		{
			page.WaitForAssertion(() => Assert.True(page.Find("input[type=file]").HasAttribute("disabled")));
			page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("second", "second.jpeg", contentType: "image/jpeg"));
			Assert.Equal("first", System.Text.Encoding.UTF8.GetString(sent!));
		}
		finally { response.SetResult(AvatarTestResponses.Json(new AvatarState(true, Guid.NewGuid()))); await submit; }
		Assert.True(page.Find("button[type=submit], form button.quiet").HasAttribute("disabled"));
	}
}
