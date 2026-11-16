namespace PlanForge.Api.Tests;

using Microsoft.Extensions.Configuration;
using PlanForge.Api.Avatars;

public sealed class AvatarQueueConfigurationTests
{
	[Fact]
	public void ExistingIdentityQueueEndpoint_IsUsedWithoutNewResourcesOrCredentials()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AzureWebJobsStorage:queueServiceUri"] = "https://existing-qa.queue.core.windows.net" }).Build();
		Assert.Equal("https://existing-qa.queue.core.windows.net/planforge-avatar-wake", AvatarQueueClientFactory.Create(config).Uri.AbsoluteUri);
	}

	[Fact]
	public void MissingStorage_DoesNotSilentlyFallbackToLocalOrNewAccount()
		=> Assert.Throws<InvalidOperationException>(() => AvatarQueueClientFactory.Create(new ConfigurationBuilder().Build()));

	[Fact]
	public void ExistingConnectionString_IsUsedForContainedEmulator()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true" }).Build();
		Assert.Equal("127.0.0.1", AvatarQueueClientFactory.Create(config).Uri.Host);
	}
}
