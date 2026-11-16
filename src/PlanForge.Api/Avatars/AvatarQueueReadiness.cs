namespace PlanForge.Api.Avatars;

using Azure;
using Azure.Storage.Queues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Proves existing storage capabilities once at startup without touching business data or logging configuration.</summary>
public sealed class AvatarQueueReadiness(QueueClient main, QueueClient recovery, ILogger<AvatarQueueReadiness>? logger = null) : BackgroundService, IAvatarQueueReadiness
{
	private bool ready;
	public bool IsReady => Volatile.Read(ref ready);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(20));
		try
		{
			foreach (QueueClient queue in new[] { main, recovery })
			{
				await queue.CreateIfNotExistsAsync(cancellationToken: timeout.Token);
				await queue.PeekMessageAsync(timeout.Token);
				var receipt = await queue.SendMessageAsync("wake", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(-1), timeout.Token);
				await queue.DeleteMessageAsync(receipt.Value.MessageId, receipt.Value.PopReceipt, timeout.Token);
			}
			Volatile.Write(ref ready, true);
			logger?.LogInformation("Avatar storage startup capability check passed.");
		}
		catch (Exception exception) when (exception is RequestFailedException or HttpRequestException or IOException or OperationCanceledException or TimeoutException)
		{
			// Preserve ordinary API availability; the existing readiness gate must not advance Web publication.
			logger?.LogWarning("Avatar storage startup capability check failed. FailureType={FailureType} Status={Status}", exception.GetType().Name, (exception as RequestFailedException)?.Status);
		}
	}
}
