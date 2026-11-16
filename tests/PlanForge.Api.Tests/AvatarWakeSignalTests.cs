namespace PlanForge.Api.Tests;

using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using PlanForge.Api.Avatars;

public sealed class AvatarWakeSignalTests
{
	[Fact]
	public async Task Signal_PersistsMinimalWakeWithDelayAndNonexpiringTtlAsync()
	{
		var client = new WakeClient();
		var signal = new QueueAvatarWakeSignal(client);
		var receipt = await signal.SendAsync(TimeSpan.FromSeconds(10));
		Assert.True(client.Created);
		Assert.Equal("wake", client.Message);
		Assert.Equal(TimeSpan.FromSeconds(10), client.Delay);
		Assert.Equal(TimeSpan.FromSeconds(-1), client.Ttl);
		Assert.Equal("message-id", receipt.MessageId);
		await signal.DeleteAsync(receipt);
		Assert.Equal("message-id:pop-receipt", client.Deleted);
	}

	private sealed class WakeClient : QueueClient
	{
		public bool Created { get; private set; }
		public string? Message { get; private set; }
		public TimeSpan? Delay { get; private set; }
		public TimeSpan? Ttl { get; private set; }
		public string? Deleted { get; private set; }
		public override Task<Response> CreateIfNotExistsAsync(IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
		{
			Created = true;
			return Task.FromResult<Response>(null!);
		}
		public override Task<Response<SendReceipt>> SendMessageAsync(string messageText, TimeSpan? visibilityTimeout = null, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
		{
			Message = messageText; Delay = visibilityTimeout; Ttl = timeToLive;
			return Task.FromResult(Response.FromValue(QueuesModelFactory.SendReceipt("message-id", DateTimeOffset.UtcNow, DateTimeOffset.MaxValue, "pop-receipt", DateTimeOffset.UtcNow), null!));
		}
		public override Task<Response> DeleteMessageAsync(string messageId, string popReceipt, CancellationToken cancellationToken = default)
		{
			Deleted = messageId + ":" + popReceipt;
			return Task.FromResult<Response>(null!);
		}
	}
}
