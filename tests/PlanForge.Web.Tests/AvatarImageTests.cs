namespace PlanForge.Web.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PlanForge.Contracts.Accounts;
using PlanForge.Web.Components.Shared;

public sealed class AvatarImageTests
{
	[Fact]
	public void UnknownInitialVersion_WaitsForAuthoritativeMetadataRatherThanReusingCachedPlaceholderUrl()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(AvatarTestResponses.Json(new AvatarState(false, Guid.Empty))));
		var image = context.Render<AvatarImage>(parameters => parameters.Add(component => component.ImageUrl, "/files/avatar").Add(component => component.Name, "NEA").Add(component => component.InitialHasAvatar, true));
		Assert.Empty(image.FindAll("img"));
		Assert.Equal("N", image.Find(".avatar").TextContent);
	}

	[Fact]
	public async Task VisibleImageFailure_ReturnsToInitialWithoutBrokenImageAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(AvatarTestResponses.Json(new AvatarState(false, Guid.Empty))));
		var image = context.Render<AvatarImage>(parameters => parameters.Add(component => component.ImageUrl, "/files/avatar").Add(component => component.Name, "NEA").Add(component => component.InitialHasAvatar, true).Add(component => component.InitialVersion, Guid.NewGuid()));
		await image.Find("img[hidden]").TriggerEventAsync("onload", new());
		await image.Find("img:not([hidden])").TriggerEventAsync("onerror", new());
		Assert.Empty(image.FindAll("img:not([hidden])"));
		Assert.Equal("N", image.Find(".avatar").TextContent);
	}

	[Fact]
	public async Task MetadataStartedBeforeConfirmedMutation_CannotRestoreStaleVersionAsync()
	{
		var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		Guid oldVersion = Guid.NewGuid(), acceptedVersion = Guid.NewGuid();
		int reads = 0;
		using var context = new ComponentTestContext(_ => { Interlocked.Increment(ref reads); return response.Task; });
		context.JSInterop.Setup<bool>("planForge.isDocumentVisible").SetResult(true);
		var image = context.Render<AvatarImage>(parameters => parameters.Add(component => component.ApiPath, "users/me/avatar").Add(component => component.ImageUrl, "/files/avatar"));
		image.WaitForAssertion(() => Assert.Equal(1, reads));
		image.Render(parameters => parameters.Add(component => component.InitialHasAvatar, true).Add(component => component.InitialVersion, acceptedVersion));
		response.SetResult(AvatarTestResponses.Json(new AvatarState(true, oldVersion)));
		await Task.Delay(100);
		Assert.Equal($"/files/avatar?v={acceptedVersion:D}", image.Find("img[hidden]").GetAttribute("src"));
	}

	[Fact]
	public async Task DifferentIdentity_NeverDisplaysPreviousIdentityImageAsync()
	{
		using var context = new ComponentTestContext(_ => Task.FromResult(AvatarTestResponses.Json(new AvatarState(false, Guid.NewGuid()))));
		var image = context.Render<AvatarImage>(parameters => parameters.Add(component => component.ApiPath, "agents/first/avatar").Add(component => component.ImageUrl, "/files/first").Add(component => component.InitialHasAvatar, true).Add(component => component.InitialVersion, Guid.NewGuid()));
		await image.Find("img[hidden]").TriggerEventAsync("onload", new());
		image.Render(parameters => parameters.Add(component => component.ApiPath, "agents/second/avatar").Add(component => component.ImageUrl, "/files/second").Add(component => component.Name, "Second"));
		Assert.Empty(image.FindAll("img:not([hidden])"));
		Assert.Equal("S", image.Find(".avatar").TextContent);
	}

	[Theory]
	[InlineData(false, true)]
	[InlineData(true, false)]
	public async Task HiddenDocumentOrSurface_DoesNotFetchMetadataAsync(bool documentVisible, bool surfaceVisible)
	{
		int reads = 0;
		using var context = new ComponentTestContext(_ => { Interlocked.Increment(ref reads); return Task.FromResult(AvatarTestResponses.Json(new AvatarState(false, Guid.Empty))); });
		context.JSInterop.Setup<bool>("planForge.isDocumentVisible").SetResult(documentVisible);
		var image = context.Render<AvatarImage>(parameters => parameters.Add(component => component.ApiPath, "users/me/avatar").Add(component => component.Visible, surfaceVisible));
		await Task.Delay(2200);
		Assert.Equal(0, reads);
	}

	[Fact]
	public async Task Disposal_StopsMetadataPollingAsync()
	{
		int reads = 0;
		using var context = new ComponentTestContext(_ => { Interlocked.Increment(ref reads); return Task.FromResult(AvatarTestResponses.Json(new AvatarState(false, Guid.Empty))); });
		context.JSInterop.Setup<bool>("planForge.isDocumentVisible").SetResult(true);
		var image = context.Render<AvatarImage>(parameters => parameters.Add(component => component.ApiPath, "users/me/avatar"));
		image.WaitForAssertion(() => Assert.True(reads > 0));
		await context.Renderer.Dispatcher.InvokeAsync(async () => await image.Instance.DisposeAsync());
		int afterDisposal = reads;
		await Task.Delay(2200);
		Assert.Equal(afterDisposal, reads);
	}

	[Fact]
	public async Task Poll_MetadataChangesPreloadSilentlyThenClearOnDeletionAsync()
	{
		Guid first = Guid.NewGuid(), second = Guid.NewGuid();
		AvatarState state = new(true, first);
		int reads = 0;
		using var context = new ComponentTestContext(request =>
		{
			Assert.Equal("/api/v1/users/me", request.RequestUri!.AbsolutePath);
			Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
			Interlocked.Increment(ref reads);
			return Task.FromResult(AvatarTestResponses.Json(state));
		});
		context.JSInterop.Setup<bool>("planForge.isDocumentVisible").SetResult(true);
		var image = context.Render<AvatarImage>(parameters => parameters.Add(component => component.ApiPath, "users/me/avatar").Add(component => component.ImageUrl, "/files/avatar").Add(component => component.Name, "NEA"));
		image.WaitForAssertion(() => Assert.Equal($"/files/avatar?v={first:D}", image.Find("img[hidden]").GetAttribute("src")));
		await image.Find("img[hidden]").TriggerEventAsync("onload", new());
		Assert.Equal($"/files/avatar?v={first:D}", image.Find("img:not([hidden])").GetAttribute("src"));
		state = new(true, second);
		image.WaitForAssertion(() => Assert.Equal($"/files/avatar?v={second:D}", image.Find("img[hidden]").GetAttribute("src")), TimeSpan.FromSeconds(5));
		Assert.Equal($"/files/avatar?v={first:D}", image.Find("img:not([hidden])").GetAttribute("src"));
		await image.Find("img[hidden]").TriggerEventAsync("onload", new());
		Assert.Equal($"/files/avatar?v={second:D}", image.Find("img:not([hidden])").GetAttribute("src"));
		state = new(false, Guid.NewGuid());
		image.WaitForAssertion(() => Assert.Empty(image.FindAll("img")), TimeSpan.FromSeconds(5));
		Assert.Equal("N", image.Find(".avatar").TextContent);
		Assert.True(reads >= 3);
		Assert.Empty(context.Services.GetService<PlanForge.Web.State.NotificationState>()!.Items);
	}

	[Fact]
	public async Task Image_IsPreloadedBeforeDisplayAndFailureLeavesInitialAsync()
	{
		Guid version = Guid.NewGuid();
		using var context = new ComponentTestContext(_ => Task.FromResult(AvatarTestResponses.Json(new AvatarState(true, version))));
		var image = context.Render<AvatarImage>(parameters => parameters
			.Add(component => component.ImageUrl, "/files/avatar")
			.Add(component => component.Name, "NEA")
			.Add(component => component.InitialHasAvatar, true)
			.Add(component => component.InitialVersion, version));
		Assert.Equal("N", image.Find(".avatar").TextContent);
		Assert.Empty(image.FindAll("img:not([hidden])"));
		Assert.Equal($"/files/avatar?v={version:D}", image.Find("img[hidden]").GetAttribute("src"));
		await image.Find("img[hidden]").TriggerEventAsync("onerror", new());
		Assert.Empty(image.FindAll("img"));
		Assert.Equal("N", image.Find(".avatar").TextContent);
	}
}
