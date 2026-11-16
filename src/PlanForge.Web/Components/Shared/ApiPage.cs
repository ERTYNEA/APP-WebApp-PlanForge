namespace PlanForge.Web.Components.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PlanForge.Web.Api;
using PlanForge.Contracts.Planning;
using PlanForge.Web.State;

public abstract class ApiPage : ComponentBase, IAsyncDisposable
{
	private readonly CancellationTokenSource lifetime = new();
	private Task? polling;
	[Inject] protected NotificationState Notifications { get; set; } = default!;
	protected ApiPage() => Token = lifetime.Token;
	[Inject] protected PlanForgeClient Api { get; set; } = default!;
	[Inject] protected NavigationManager Navigation { get; set; } = default!;
	[Inject] protected IJSRuntime JavaScript { get; set; } = default!;
	protected bool Busy { get; private set; }
	protected bool Conflict { get; private set; }
	protected virtual bool HasOpenOperation => false;
	protected virtual bool UsesInlineErrors => HasOpenOperation;
	private bool pollingRefresh;
	protected string? Error { get; set; }
	protected CancellationToken Token { get; }
	protected IReadOnlyList<ToastNotification> Toasts => Notifications.Items;
	protected void ClearFeedback() { Error = null; Conflict = false; }

	protected async Task ExecuteAsync(Func<Task> action, string? message = null, NotificationKind kind = NotificationKind.Success)
	{
		if (Busy) return;
		Busy = true; Error = null; Conflict = false;
		try { await action(); if (message is not null) Notify(message, kind); }
		catch (OperationCanceledException) when (Token.IsCancellationRequested) { /* View disposed. */ }
		catch (ApiException exception)
		{
			Conflict = exception.StatusCode == 409;
			Error = exception.Message + (exception.CorrelationId is null ? "" : " " + string.Format(UiText.Get("Reference: {0}"), exception.CorrelationId));
			if (exception.StatusCode == 401) Navigation.NavigateTo("/login", forceLoad: true);
		}
		catch (JSException) { Error = UiText.Get("The export was not downloaded. Reload before retrying."); }
		catch (HttpRequestException) { Error = UiText.Get("The service is temporarily unavailable. Your changes have not been confirmed. Reload before retrying."); }
		catch (IOException) { Error = UiText.Get("The file could not be read. Check its size and try again."); }
		catch (OperationCanceledException) { Error = UiText.Get("The request timed out. Reload to check its result before retrying."); }
		finally
		{
			Busy = false;
			if (Error is not null && !UsesInlineErrors && !pollingRefresh && !Token.IsCancellationRequested)
			{
				Notify(Conflict ? Error + " " + UiText.Get("Reload and reconcile. No changes were overwritten.") : Error, NotificationKind.Danger);
				Error = null;
			}
		}
	}
	protected void Notify(ProjectChangeInfo change)
	{
		NotificationKind kind = change.Operation switch
		{
			ProjectChangeOperation.Destructive => NotificationKind.Danger,
			ProjectChangeOperation.Import or ProjectChangeOperation.Export => NotificationKind.Success,
			_ => NotificationKind.Neutral
		};
		Notifications.Publish(string.Format(UiText.Get("{0} — {1}"), UiText.Get(change.Action), change.ActorName), kind, change.Id);
	}

	protected void Notify(string message, NotificationKind kind = NotificationKind.Success) => Notifications.Publish(message, kind);

	protected async Task RefreshConfirmedAsync(Func<Task> refresh)
	{
		try { await refresh(); }
		catch (Exception exception) when (exception is ApiException or HttpRequestException or IOException or OperationCanceledException)
		{
			Error = UiText.Get("Your changes were confirmed, but the view could not be refreshed. Reload to see the latest data.");
		}
	}

	protected static async Task<byte[]> ReadFileAsync(IBrowserFile file, int maximumBytes, CancellationToken token)
	{
		await using Stream stream = file.OpenReadStream(maximumBytes, token);
		using var content = new MemoryStream();
		await stream.CopyToAsync(content, token);
		return content.ToArray();
	}
	protected void StartPolling(Func<Task> refresh)
	{
		polling ??= PollAsync(refresh);
	}
	private async Task PollAsync(Func<Task> refresh)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
		try
		{
			while (await timer.WaitForNextTickAsync(Token))
				await InvokeAsync(async () => { if (!Busy && !HasOpenOperation) { pollingRefresh = true; try { await ExecuteAsync(refresh); } finally { pollingRefresh = false; } } StateHasChanged(); });
		}
		catch (OperationCanceledException) when (Token.IsCancellationRequested) { /* Expected navigation cancellation. */ }
	}
	public async ValueTask DisposeAsync()
	{
		await lifetime.CancelAsync();
		if (polling is not null) await polling;
		lifetime.Dispose();
		GC.SuppressFinalize(this);
	}
}
