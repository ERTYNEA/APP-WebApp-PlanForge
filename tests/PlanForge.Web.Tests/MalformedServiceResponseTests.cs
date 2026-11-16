namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Web.Components.Shared;
using PlanForge.Web.State;
using System.Text.Json;

public sealed class MalformedServiceResponseTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SuccessfulHttpWithMalformedJson_IsContainedAtServiceBoundaryAsync(bool confirmed)
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
		{
			Content = new StringContent("{ PRIVATE-MALFORMED-RESPONSE", System.Text.Encoding.UTF8, "application/json")
		}));
		var probe = context.Render<ResponseProbe>();
		await probe.InvokeAsync(() => probe.Instance.LoadAsync(confirmed));
		var notifications = context.Services.GetRequiredService<NotificationState>().Items;
		Assert.Contains(notifications, toast => toast.Kind == NotificationKind.Danger && toast.Message.Contains(confirmed
			? "Your changes were confirmed, but the view could not be refreshed"
			: "The service returned an invalid response. Reload to check its result before retrying.", StringComparison.Ordinal));
		Assert.DoesNotContain(notifications, toast => toast.Message.Contains("PRIVATE", StringComparison.Ordinal));
		Assert.Equal(confirmed, notifications.Any(toast => toast.Kind == NotificationKind.Success));
	}

	[Fact]
	public async Task ProgrammingJsonException_IsNotSwallowedByGenericPageWrapperAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(new HttpResponseMessage()));
		var probe = context.Render<ResponseProbe>();
		await Assert.ThrowsAsync<JsonException>(() => probe.InvokeAsync(() => probe.Instance.ProgrammingFaultAsync()));
	}

	private sealed class ResponseProbe : ApiPage
	{
		public Task LoadAsync(bool confirmed) => ExecuteAsync(async () =>
		{
			if (confirmed)
			{
				Notify("Confirmed test write");
				await RefreshConfirmedAsync(async () => await Api.GetAsync<object>("controlled", Token));
			}
			else
				await Api.GetAsync<object>("controlled", Token);
		});
		public Task ProgrammingFaultAsync() => ExecuteAsync(() => throw new JsonException("Controlled programming failure"));
	}
}
