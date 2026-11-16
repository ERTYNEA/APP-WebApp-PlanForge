namespace PlanForge.Api.Avatars;

using Azure.Core;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;

public static class AvatarQueueClientFactory
{
	public static QueueClient Create(IConfiguration configuration)
	{
		var options = new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 };
		options.Retry.Mode = RetryMode.Exponential;
		options.Retry.MaxRetries = 3;
		options.Retry.Delay = TimeSpan.FromMilliseconds(200);
		options.Retry.MaxDelay = TimeSpan.FromSeconds(1);
		options.Retry.NetworkTimeout = TimeSpan.FromSeconds(3);
		string? connection = configuration["AzureWebJobsStorage"];
		if (!string.IsNullOrWhiteSpace(connection))
			return new QueueClient(connection, QueueAvatarWakeSignal.QueueName, options);
		string? endpoint = configuration["AzureWebJobsStorage:queueServiceUri"];
		string? account = configuration["AzureWebJobsStorage:accountName"];
		if (string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(account) && account.All(character => char.IsAsciiLetterOrDigit(character)))
			endpoint = "https://" + account + ".queue.core.windows.net";
		if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query))
			throw new InvalidOperationException("Existing Functions storage must provide a queue-capable connection or identity endpoint.");
		return new QueueClient(new Uri(uri.AbsoluteUri.TrimEnd('/') + "/" + QueueAvatarWakeSignal.QueueName), new ManagedIdentityCredential(SelectManagedIdentity(configuration)), options);
	}

	/// <summary>Selects the host storage identity without acquiring a cloud token.</summary>
	public static ManagedIdentityId SelectManagedIdentity(IConfiguration configuration)
	{
		string? clientId = configuration["AzureWebJobsStorage:clientId"];
		string? resourceId = configuration["AzureWebJobsStorage:managedIdentityResourceId"];
		if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(resourceId))
			throw new InvalidOperationException("Functions storage clientId and managedIdentityResourceId are mutually exclusive.");
		if (!string.IsNullOrEmpty(resourceId))
			return ManagedIdentityId.FromUserAssignedResourceId(new ResourceIdentifier(resourceId));
		return string.IsNullOrEmpty(clientId) ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(clientId);
	}
}
