namespace PlanForge.Web.Components.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PlanForge.Web.Api;

public abstract class ApiPage : ComponentBase, IAsyncDisposable
{
	private readonly CancellationTokenSource lifetime = new();
	private Task? polling;
	private readonly List<Task> toastTasks = [];
	protected ApiPage() => Token = lifetime.Token;
	[Inject] protected PlanForgeClient Api { get; set; } = default!;
	[Inject] protected NavigationManager Navigation { get; set; } = default!;
	[Inject] protected IJSRuntime JavaScript { get; set; } = default!;
	protected bool Busy { get; private set; }
	protected bool Conflict { get; private set; }
	protected string? Error { get; set; }
	protected CancellationToken Token { get; }
	protected List<string> Toasts { get; } = [];

	protected async Task ExecuteAsync(Func<Task> action, string? message = null)
	{
		if (Busy) return;
		Busy = true; Error = null; Conflict = false;
		try { await action(); if (message is not null) Notify(message); }
		catch (OperationCanceledException) when (Token.IsCancellationRequested) { /* View disposed. */ }
		catch (ApiException exception)
		{
			Conflict = exception.StatusCode == 409;
			Error = exception.Message + (exception.CorrelationId is null ? "" : " " + string.Format(UiText.Get("Reference: {0}"), exception.CorrelationId));
			if (exception.StatusCode == 401) Navigation.NavigateTo("/login", forceLoad: true);
		}
		catch (HttpRequestException) { Error = UiText.Get("The service is temporarily unavailable. Your changes have not been confirmed. Reload before retrying."); }
		catch (IOException) { Error = UiText.Get("The file could not be read. Check its size and try again."); }
		catch (OperationCanceledException) { Error = UiText.Get("The request timed out. Reload to check its result before retrying."); }
		finally { Busy = false; }
	}
	private readonly Dictionary<string, long> toastVersions = [];
	private long toastSequence;
	protected void Notify(string message)
	{
		message = UiText.Get(message);
		long sequence = ++toastSequence;
		toastVersions[message] = sequence;
		Toasts.Remove(message);
		Toasts.Add(message);
		if (Toasts.Count > 4) { toastVersions.Remove(Toasts[0]); Toasts.RemoveAt(0); }
		toastTasks.RemoveAll(task => task.IsCompleted);
		toastTasks.Add(ExpireToastAsync(message, sequence));
	}
	private async Task ExpireToastAsync(string message, long sequence)
	{
		try
		{
			await Task.Delay(TimeSpan.FromSeconds(6), Token);
			await InvokeAsync(() =>
			{
				if (!Token.IsCancellationRequested && toastVersions.TryGetValue(message, out long current) && current == sequence) { toastVersions.Remove(message); Toasts.Remove(message); StateHasChanged(); }
			});
		}
		catch (OperationCanceledException) when (Token.IsCancellationRequested) { /* View closed before toast expiry. */ }
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
				await InvokeAsync(async () => { if (!Busy) await ExecuteAsync(refresh); StateHasChanged(); });
		}
		catch (OperationCanceledException) when (Token.IsCancellationRequested) { /* Expected navigation cancellation. */ }
	}
	public async ValueTask DisposeAsync()
	{
		await lifetime.CancelAsync();
		if (polling is not null) await polling;
		await Task.WhenAll(toastTasks);
		lifetime.Dispose();
		GC.SuppressFinalize(this);
	}
}
