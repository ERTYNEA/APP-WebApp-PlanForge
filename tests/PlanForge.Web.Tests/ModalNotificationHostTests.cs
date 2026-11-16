namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;

public sealed class ModalNotificationHostTests
{
	[Fact]
	public async Task NativeNestedModal_UsesOnlyInnermostHostAndReturnsStateOnCloseAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage()));
		var shell = context.Render<ModalHarness>();
		var notifications = context.Services.GetRequiredService<NotificationState>();
		await shell.InvokeAsync(() => notifications.Publish("Before opening", NotificationKind.Success));
		Assert.Single(shell.FindAll(".toast-stack"));
		await shell.InvokeAsync(() => shell.Instance.SetModals(true, false));
		shell.WaitForAssertion(() => Assert.Single(shell.FindAll("dialog .toast-stack")));
		Assert.Single(shell.FindAll(".toast-stack"));
		await shell.InvokeAsync(() => shell.Instance.SetModals(true, true));
		shell.WaitForAssertion(() => Assert.Single(shell.FindAll("dialog dialog .toast-stack")));
		Assert.Single(shell.FindAll(".toast-stack"));
		await shell.InvokeAsync(() => notifications.Publish("Nested operation", NotificationKind.Danger));
		shell.WaitForAssertion(() => Assert.Equal(2, shell.FindAll("dialog dialog .toast").Count));
		await shell.InvokeAsync(() => shell.Instance.SetModals(true, false));
		shell.WaitForAssertion(() => Assert.Single(shell.FindAll("dialog .toast-stack")));
		await shell.InvokeAsync(() => shell.Instance.SetModals(false, false));
		shell.WaitForAssertion(() => Assert.Empty(shell.FindAll("dialog")));
		Assert.Single(shell.FindAll(".toast-stack"));
		Assert.Equal(2, shell.FindAll(".toast").Count);
	}

	[Fact]
	public async Task HostHandoff_DoesNotReplayOldAnnouncementsOrCreateMultipleLiveRegionsAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage()));
		var shell = context.Render<ModalHarness>();
		var notifications = context.Services.GetRequiredService<NotificationState>();
		await shell.InvokeAsync(() => { notifications.Publish("First", NotificationKind.Success); notifications.Publish("Second", NotificationKind.Danger); });
		Assert.Single(shell.FindAll("[role=status]"));
		await shell.InvokeAsync(() => shell.Instance.SetModals(true, false));
		shell.WaitForAssertion(() => Assert.Single(shell.FindAll("dialog [role=status]")));
		Assert.Equal("", shell.Find("[role=status]").TextContent.Trim());
		Assert.Equal(2, shell.FindAll(".toast").Count);
		await shell.InvokeAsync(() => notifications.Publish("New modal result", NotificationKind.Neutral));
		shell.WaitForAssertion(() => Assert.Contains("New modal result", shell.Find("[role=status]").TextContent));
		Assert.DoesNotContain("First", shell.Find("[role=status]").TextContent);
		await shell.InvokeAsync(() => shell.Instance.SetModals(true, true));
		shell.WaitForAssertion(() => Assert.Single(shell.FindAll("dialog dialog [role=status]")));
		Assert.Equal("", shell.Find("[role=status]").TextContent.Trim());
		await shell.InvokeAsync(() => shell.Instance.SetModals(false, false));
		shell.WaitForAssertion(() => Assert.Empty(shell.FindAll("dialog")));
		Assert.Single(shell.FindAll("[role=status]"));
		Assert.Equal("", shell.Find("[role=status]").TextContent.Trim());
		await shell.InvokeAsync(() => notifications.Publish("New modal result", NotificationKind.Neutral));
		Assert.Equal("", shell.Find("[role=status]").TextContent.Trim());
		Assert.Equal(3, notifications.Items.Count);
	}

	public sealed class ModalHarness : ComponentBase
	{
		private bool outer, inner;
		public void SetModals(bool outer, bool inner) { this.outer = outer; this.inner = inner; StateHasChanged(); }
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<NotificationRegion>(0);
			builder.CloseComponent();
			if (!outer) return;
			builder.OpenComponent<Modal>(1);
			builder.AddAttribute(2, "Title", "Outer");
			builder.AddAttribute(3, "ChildContent", (RenderFragment)(content =>
			{
				if (!inner) return;
				content.OpenComponent<Modal>(0);
				content.AddAttribute(1, "Title", "Inner");
				content.CloseComponent();
			}));
			builder.CloseComponent();
		}
	}
}
