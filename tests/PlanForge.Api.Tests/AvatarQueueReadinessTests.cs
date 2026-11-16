namespace PlanForge.Api.Tests;

using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using PlanForge.Api.Avatars;

public sealed class AvatarQueueReadinessTests
{
	[Theory]
	[InlineData(false, "peek")]
	[InlineData(true, "delete")]
	[InlineData(false, "timeout")]
	public async Task Startup_WhenExistingRightsOrStorageAreUnavailable_RemainsUnreadyWithoutCrashingHostAsync(bool recoveryFails, string stage)
	{
		var main = new ProbeQueue { FailAt = recoveryFails ? null : stage };
		var recovery = new ProbeQueue { FailAt = recoveryFails ? stage : null };
		using var readiness = new AvatarQueueReadiness(main, recovery);
		await readiness.StartAsync(CancellationToken.None);
		await readiness.ExecuteTask!;
		Assert.False(readiness.IsReady);
	}

	[Fact]
	public async Task Startup_ProvesExistingMainAndRecoveryQueueCapabilitiesBeforeReadyAsync()
	{
		var main = new ProbeQueue();
		var recovery = new ProbeQueue();
		using var readiness = new AvatarQueueReadiness(main, recovery);
		Assert.False(readiness.IsReady);
		await readiness.StartAsync(CancellationToken.None);
		await readiness.ExecuteTask!;
		Assert.True(readiness.IsReady);
		Assert.Equal(new[] { "create", "peek", "send:wake", "delete" }, main.Calls);
		Assert.Equal(main.Calls, recovery.Calls);
	}

	private sealed class ProbeQueue : QueueClient
	{
		public List<string> Calls { get; } = [];
		public string? FailAt { get; init; }
		public override Task<Response> CreateIfNotExistsAsync(IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
		{
			Calls.Add("create");
			return Task.FromResult<Response>(null!);
		}
		public override Task<Response<PeekedMessage>> PeekMessageAsync(CancellationToken cancellationToken = default)
		{
			if (FailAt == "peek")
				throw new RequestFailedException(403, "Synthetic unavailable capability");
			if (FailAt == "timeout")
				throw new OperationCanceledException();
			Calls.Add("peek");
			return Task.FromResult(Response.FromValue<PeekedMessage>(null!, null!));
		}
		public override Task<Response<SendReceipt>> SendMessageAsync(string messageText, TimeSpan? visibilityTimeout = null, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
		{
			Calls.Add("send:" + messageText);
			Assert.True(visibilityTimeout >= TimeSpan.FromSeconds(20));
			return Task.FromResult(Response.FromValue(QueuesModelFactory.SendReceipt("own-probe", DateTimeOffset.UtcNow, DateTimeOffset.MaxValue, "own-receipt", DateTimeOffset.UtcNow), null!));
		}
		public override Task<Response> DeleteMessageAsync(string messageId, string popReceipt, CancellationToken cancellationToken = default)
		{
			Assert.Equal("own-probe", messageId);
			Assert.Equal("own-receipt", popReceipt);
			if (FailAt == "delete")
				throw new RequestFailedException(403, "Synthetic unavailable capability");
			Calls.Add("delete");
			return Task.FromResult<Response>(null!);
		}
	}
}
