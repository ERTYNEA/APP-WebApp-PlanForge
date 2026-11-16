namespace PlanForge.Api.Avatars;

using Azure;
using Azure.Storage.Queues;
using PlanForge.Application.Accounts;

/// <summary>Uses the existing Functions storage account only; SQL remains the source of work and bytes.</summary>
public sealed class QueueAvatarWakeSignal(QueueClient queue) : IAvatarWakeSignal
{
	public const string QueueName = "planforge-avatar-wake";

	public async Task<AvatarWakeReceipt> SendAsync(TimeSpan delay, CancellationToken cancellationToken = default)
	{
		await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
		var receipt = await queue.SendMessageAsync("wake", delay, TimeSpan.FromSeconds(-1), cancellationToken);
		return new AvatarWakeReceipt(receipt.Value.MessageId, receipt.Value.PopReceipt);
	}

	public async Task DeleteAsync(AvatarWakeReceipt receipt, CancellationToken cancellationToken = default)
	{
		try
		{
			await queue.DeleteMessageAsync(receipt.MessageId, receipt.PopReceipt, cancellationToken);
		}
		catch (RequestFailedException exception) when (exception.Status is 404 or 400 && exception.ErrorCode is "MessageNotFound" or "PopReceiptMismatch")
		{
			// A visible successor may already have been consumed by another invocation, which creates its own successor first.
			return;
		}
	}
}
