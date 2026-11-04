namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Agents;
using PlanForge.Web.Components.Shared;

public sealed class AgentTokenPanelTests
{
	[Fact]
	public async Task Revoke_WhenConfirmed_UsesCredentialIdAndRefreshesMetadataAsync()
	{
		Guid id=Guid.NewGuid();
		var credential=new AgentTokenProfile(id,"Automation",null,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddDays(30),null,null);
		string? path=null;
		using var context=new ComponentTestContext(request=> { if(request.Method==HttpMethod.Delete) { path=request.RequestUri!.AbsolutePath; return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent)); } return Task.FromResult(ComponentTestContext.Json(new[]{credential})); });
		var page=context.Render<AgentTokenPanel>();
		await page.FindAll("button").Single(button=>button.TextContent=="Revoke").ClickAsync(new());
		Assert.Null(path);
		page.Find("#confirm-intent").Change(true);
		await page.Find("#confirm-delete").ClickAsync(new());
		Assert.Equal($"/api/v1/agent-tokens/{id}",path);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Issue_WhenCreatedOrRenewed_ShowsNewSecretOnlyUntilDismissedAsync(bool renew)
	{
		Guid id=Guid.NewGuid();
		string secret="pf_a_"+new string('c',64);
		var credential=new AgentTokenProfile(id,"Automation",null,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddDays(30),null,null);
		string? path=null;
		using var context=new ComponentTestContext(request=> { if(request.Method==HttpMethod.Post) path=request.RequestUri!.AbsolutePath; return Task.FromResult(ComponentTestContext.Json(request.Method==HttpMethod.Get ? (object)new[]{credential} : new AgentTokenGrant(secret,credential))); });
		var page=context.Render<AgentTokenPanel>();
		Assert.DoesNotContain(secret,page.Markup);
		await page.FindAll("button").Single(button=>button.TextContent==(renew ? "Renew" : "+ Create token")).ClickAsync(new());
		if(!renew) { page.Find("#token-name").Change("Automation"); await page.Find("#token-form").SubmitAsync(); }
		Assert.Equal(renew ? $"/api/v1/agent-tokens/{id}/renew" : "/api/v1/agent-tokens",path);
		Assert.Contains(secret,page.Markup);
		Assert.Contains("shown only once",page.Markup);
		await page.Find("#dismiss-token").ClickAsync(new());
		Assert.DoesNotContain(secret,page.Markup);
	}
}
