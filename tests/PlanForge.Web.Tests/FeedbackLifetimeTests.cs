namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using PlanForge.Web.Components.Shared;

public sealed class FeedbackLifetimeTests
{
	[Fact]
	public async Task Notify_WhenRepeated_CoalescesAndExpiresWithoutBlockingControlsAsync()
	{
		using var context=new ComponentTestContext(_=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent)));
		var page=context.Render<ToastProbe>();
		await page.InvokeAsync(()=> { page.Instance.Publish("Project updated"); page.Instance.Publish("Project updated"); });
		Assert.Single(page.FindAll(".toast"));
		page.WaitForAssertion(()=>Assert.Empty(page.FindAll(".toast")),TimeSpan.FromSeconds(9));
	}
	private sealed class ToastProbe : ApiPage
	{
		public void Publish(string value) { Notify(value); StateHasChanged(); }
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<Feedback>(0); builder.AddAttribute(1,"Toasts",Toasts); builder.CloseComponent();
		}
	}
}
