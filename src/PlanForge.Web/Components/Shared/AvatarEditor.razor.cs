namespace PlanForge.Web.Components.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PlanForge.Contracts.Accounts;
using PlanForge.Web.Api;
using PlanForge.Web.State;

public partial class AvatarEditor
{
	private const int maximumBytes = 4 * 1024 * 1024;
	private AvatarState current = new(false, Guid.Empty);
	private IBrowserFile? file;
	private (string Path, bool HasAvatar, Guid Version) previous;

	[Parameter] public string ApiPath { get; set; } = "";
	[Parameter] public EventCallback<bool> BusyChanged { get; set; }
	[Parameter] public EventCallback<AvatarState> Changed { get; set; }
	[Parameter] public bool Deferred { get; set; }
	[Parameter] public bool Disabled { get; set; }
	[Parameter] public string? ExternalError { get; set; }
	[Parameter] public bool HasAvatar { get; set; }
	public bool HasPending => file is not null;
	[Parameter] public string ImageUrl { get; set; } = "";
	[Parameter] public string Name { get; set; } = "";
	[Parameter] public Guid Version { get; set; }
	protected override bool UsesInlineErrors => true;

	/// <summary>Persists the selected avatar after its owning agent has been saved.</summary>
	/// <param name="path">The authenticated avatar endpoint for the saved identity.</param>
	/// <returns>Whether the view refreshed, so the caller can retain a confirmed-refresh warning.</returns>
	public async Task<bool> UploadPendingAsync(string path)
	{
		if (file is null)
			return true;
		ClearFeedback();
		byte[] bytes = await ReadFileAsync(file, maximumBytes, Token);
		AvatarState state = await Api.SendAvatarAsync(HttpMethod.Put, path, bytes: bytes, contentType: file.ContentType, cancellationToken: Token);
		current = state ?? throw new ApiException(502, UiText.Get("The service returned an empty response."));
		file = null;
		Notify("Avatar updated");
		await RefreshConfirmedAsync(() => Changed.InvokeAsync(current));
		return Error is null;
	}

	protected override void OnParametersSet()
	{
		if (previous == (ApiPath, HasAvatar, Version))
			return;
		previous = (ApiPath, HasAvatar, Version);
		current = new(HasAvatar, Version);
	}

	private Task ExecuteMutationAsync(Func<Task> action) => ExecuteAsync(async () =>
	{
		try
		{
			await BusyChanged.InvokeAsync(true);
			await action();
		}
		finally
		{
			await BusyChanged.InvokeAsync(false);
		}
	});

	private Task OnRemoveAsync() => Deferred || Disabled ? Task.CompletedTask : ExecuteMutationAsync(async () =>
	{
		AvatarState state = await Api.SendAvatarAsync(HttpMethod.Delete, ApiPath, cancellationToken: Token);
		current = state ?? throw new ApiException(502, UiText.Get("The service returned an empty response."));
		Notify("Avatar removed", NotificationKind.Danger);
		await RefreshConfirmedAsync(() => Changed.InvokeAsync(current));
	});

	private void OnStateChanged(AvatarState state) => current = state;

	private Task OnUploadAsync() => Deferred || Disabled ? Task.CompletedTask : ExecuteMutationAsync(() => UploadPendingAsync(ApiPath));
}
