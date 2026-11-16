namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using PlanForge.Web.Components.Shared;

public sealed class FeedbackLifetimeTests
{
	[Fact]
	public async Task Notify_WhenRepeated_CoalescesAndExpiresWithoutBlockingControlsAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent)));
		var region = context.Render<NotificationRegion>();
		var page = context.Render<ToastProbe>();
		await page.InvokeAsync(() => { page.Instance.Publish("Project updated"); page.Instance.Publish("Project updated"); });
		Assert.Single(region.FindAll(".toast"));
		region.WaitForAssertion(() => Assert.Empty(region.FindAll(".toast")), TimeSpan.FromSeconds(9));
	}
	private sealed class ToastProbe : ApiPage
	{
		public void Publish(string value) { Notify(value); StateHasChanged(); }
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.AddContent(0, "Page content");
		}
	}
}
