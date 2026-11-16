namespace PlanForge.Api.Tests;

using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using PlanForge.Api.Avatars;

public sealed class AvatarQueueIdentityTests
{
	[Fact]
	public void ConflictingIdentitySelectors_AreRejectedBeforeClientCreation()
	{
		IConfiguration configuration = Configuration("11111111-1111-1111-1111-111111111111", "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/qa/providers/Microsoft.ManagedIdentity/userAssignedIdentities/queue");
		Assert.Throws<InvalidOperationException>(() => AvatarQueueClientFactory.Create(configuration));
	}

	[Fact]
	public void ResourceIdSelector_UsesThatUserAssignedIdentityWithoutTokenAcquisition()
	{
		const string resourceId = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/qa/providers/Microsoft.ManagedIdentity/userAssignedIdentities/queue";
		IConfiguration configuration = Configuration(resourceId: resourceId);
		Assert.Equal(ManagedIdentityId.FromUserAssignedResourceId(new ResourceIdentifier(resourceId)).ToString(), AvatarQueueClientFactory.SelectManagedIdentity(configuration).ToString());
		Assert.Equal("https://existing-qa.queue.core.windows.net/planforge-avatar-wake", AvatarQueueClientFactory.Create(configuration).Uri.AbsoluteUri);
	}

	[Fact]
	public void ClientIdSelector_UsesThatUserAssignedIdentityWithoutTokenAcquisition()
	{
		const string clientId = "11111111-1111-1111-1111-111111111111";
		Assert.Equal(ManagedIdentityId.FromUserAssignedClientId(clientId).ToString(), AvatarQueueClientFactory.SelectManagedIdentity(Configuration(clientId: clientId)).ToString());
	}

	[Fact]
	public void NoUserSelector_UsesSystemAssignedIdentityWithoutTokenAcquisition()
		=> Assert.Equal(ManagedIdentityId.SystemAssigned.ToString(), AvatarQueueClientFactory.SelectManagedIdentity(Configuration()).ToString());

	private static IConfiguration Configuration(string? clientId = null, string? resourceId = null)
		=> new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["AzureWebJobsStorage:queueServiceUri"] = "https://existing-qa.queue.core.windows.net",
			["AzureWebJobsStorage:credential"] = "managedidentity",
			["AzureWebJobsStorage:clientId"] = clientId,
			["AzureWebJobsStorage:managedIdentityResourceId"] = resourceId
		}).Build();
}
