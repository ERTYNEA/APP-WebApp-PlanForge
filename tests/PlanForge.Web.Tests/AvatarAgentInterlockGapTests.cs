namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using PlanForge.Contracts.Accounts;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Pages;
using System.Net;

public sealed class AvatarAgentInterlockGapTests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public async Task IndependentAvatarMutation_DisablesAndGuardsParentSaveAndDeleteUntilCompleteAsync(bool remove, bool fail)
	{
		Guid id = Guid.NewGuid();
		var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		int avatarWrites = 0, agentWrites = 0;
		using var context = new ComponentTestContext(request =>
		{
			string path = request.RequestUri!.AbsolutePath;
			if (request.Method != HttpMethod.Get)
			{
				if (path.EndsWith("/avatar", StringComparison.Ordinal))
				{
					avatarWrites++;
					return response.Task;
				}
				agentWrites++;
				return Task.FromResult(ComponentTestContext.Json(new AgentProfile(id, "Agent", "", true, remove)));
			}
			return Task.FromResult(ComponentTestContext.Json(path.EndsWith("/agent-tokens", StringComparison.Ordinal)
				? (object)Array.Empty<AgentTokenProfile>() : new[] { new AgentProfile(id, "Agent", "", true, remove) }));
		});
		var page = context.Render<Agents>();
		await page.FindAll("button").Single(button => button.TextContent == "Edit").ClickAsync(new());
		Assert.False(page.FindAll("button").Single(button => button.TextContent == "Save agent").HasAttribute("disabled"));
		Assert.False(page.FindAll("button").Single(button => button.TextContent == "Delete agent").HasAttribute("disabled"));
		if (!remove)
			page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1], "avatar.png", contentType: "image/png"));
		Task mutation = remove
			? page.FindAll("button").Single(button => button.TextContent == "Remove avatar").ClickAsync(new())
			: page.Find(".avatar-editor form").SubmitAsync();
		try
		{
			page.WaitForAssertion(() => Assert.Equal(1, avatarWrites));
			Assert.True(page.FindAll("button").Single(button => button.TextContent == "Save agent").HasAttribute("disabled"));
			Assert.True(page.FindAll("button").Single(button => button.TextContent == "Delete agent").HasAttribute("disabled"));
			// Dispatch anyway: bUnit can bypass native disabled controls, as queued events can.
			await page.Find("#agent-form").SubmitAsync();
			await page.FindAll("button").Single(button => button.TextContent == "Delete agent").ClickAsync(new());
			Assert.Equal(0, agentWrites);
			Assert.NotEmpty(page.FindAll("#agent-form"));
			Assert.Empty(page.FindAll("#confirm-intent"));
		}
		finally
		{
			response.SetResult(fail
				? ComponentTestContext.Json(new { detail = "Avatar mutation rejected." }, HttpStatusCode.BadRequest)
				: AvatarTestResponses.Json(new AvatarState(!remove, Guid.NewGuid())));
			await mutation;
		}
		Assert.False(page.FindAll("button").Single(button => button.TextContent == "Save agent").HasAttribute("disabled"));
		Assert.False(page.FindAll("button").Single(button => button.TextContent == "Delete agent").HasAttribute("disabled"));
		if (fail)
			Assert.Contains("Avatar mutation rejected.", Assert.Single(page.FindAll("[role=alert]")).TextContent);
	}
}
