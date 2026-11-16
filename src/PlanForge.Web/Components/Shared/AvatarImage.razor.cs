namespace PlanForge.Web.Components.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PlanForge.Contracts.Accounts;
using PlanForge.Web.Api;

public partial class AvatarImage : IAsyncDisposable
{
	private readonly CancellationTokenSource lifetime = new();
	private string? displayedUrl, pendingUrl;
	private Task? polling;
	private long revision;
	private (string Path, string Url, bool HasAvatar, Guid Version)? previous;
	private AvatarState? current;

	[Inject] private PlanForgeClient Api { get; set; } = default!;
	[Inject] private IJSRuntime JavaScript { get; set; } = default!;
	[Parameter] public string ApiPath { get; set; } = "";
	[Parameter] public string Class { get; set; } = "";
	[Parameter] public string ImageUrl { get; set; } = "";
	[Parameter] public bool ImageOnly { get; set; }
	[Parameter] public bool InitialHasAvatar { get; set; }
	[Parameter] public Guid InitialVersion { get; set; }
	[Parameter] public string Name { get; set; } = "";
	[Parameter] public EventCallback<AvatarState> StateChanged { get; set; }
	[Parameter] public bool Visible { get; set; } = true;

	public async ValueTask DisposeAsync()
	{
		await lifetime.CancelAsync();
		if (polling is not null)
			await polling;
		lifetime.Dispose();
		GC.SuppressFinalize(this);
	}

	protected override Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
			polling = PollAsync();
		return Task.CompletedTask;
	}

	protected override void OnParametersSet()
	{
		if (previous == (ApiPath, ImageUrl, InitialHasAvatar, InitialVersion))
			return;
		if (previous is { } target && (target.Path != ApiPath || target.Url != ImageUrl))
		{
			displayedUrl = null;
			pendingUrl = null;
		}
		revision++;
		previous = (ApiPath, ImageUrl, InitialHasAvatar, InitialVersion);
		if (InitialHasAvatar && InitialVersion == Guid.Empty)
			return;
		ApplyState(new(InitialHasAvatar, InitialVersion));
	}

	private void ApplyState(AvatarState state)
	{
		current = state;
		if (!state.HasAvatar)
		{
			displayedUrl = null;
			pendingUrl = null;
		}
		else
		{
			string url = ImageUrl + (ImageUrl.Contains('?') ? "&" : "?") + "v=" + state.Version.ToString("D");
			if (url != displayedUrl)
				pendingUrl = url;
		}
	}

	private void OnDisplayFailed(string shown)
	{
		if (displayedUrl == shown)
			displayedUrl = null;
	}

	private void OnFailed(string candidate)
	{
		if (pendingUrl == candidate)
			pendingUrl = null;
	}

	private void OnLoaded(string candidate)
	{
		if (pendingUrl != candidate)
			return;
		displayedUrl = candidate;
		pendingUrl = null;
	}

	private async Task PollAsync()
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
		try
		{
			do
			{
				await InvokeAsync(RefreshAsync);
			}
			while (await timer.WaitForNextTickAsync(lifetime.Token));
		}
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
		{
			// Navigation disposes the component and its metadata loop.
		}
	}

	private async Task RefreshAsync()
	{
		if (!Visible || string.IsNullOrWhiteSpace(ApiPath))
			return;
		try
		{
			// Resolve visibility on every call; interop caches functions, so never bind a primitive snapshot.
			if (!await JavaScript.InvokeAsync<bool>("planForge.isDocumentVisible", lifetime.Token))
				return;
			string path = ApiPath;
			long startedRevision = revision;
			AvatarState state = await Api.GetAvatarStateAsync(path, lifetime.Token);
			if (path != ApiPath || startedRevision != revision || lifetime.IsCancellationRequested)
				return;
			bool changed = current != state;
			ApplyState(state);
			if (changed)
				await StateChanged.InvokeAsync(state);
			StateHasChanged();
		}
		catch (Exception exception) when (exception is ApiException or HttpRequestException or IOException or OperationCanceledException or JSException or InvalidOperationException)
		{
			// A disconnected/hidden circuit or a transient read failure must not change the last confirmed image or announce an operation.
		}
	}
}
