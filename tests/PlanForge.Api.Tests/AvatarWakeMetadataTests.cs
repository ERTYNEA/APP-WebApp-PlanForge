namespace PlanForge.Api.Tests;

using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using PlanForge.Api.Avatars;
using System.Text.Json;

public sealed class AvatarWakeMetadataTests
{
	[Theory]
	[InlineData("AvatarWake", "planforge-avatar-wake")]
	[InlineData("AvatarWakePoison", "planforge-avatar-wake-poison")]
	public async Task CompiledQueueTrigger_HasExactBindingContractAsync(string functionName, string queueName)
	{
		Type providerType = Assert.Single(typeof(AvatarWakeFunctions).Assembly.GetTypes(), type => typeof(IFunctionMetadataProvider).IsAssignableFrom(type));
		var provider = (IFunctionMetadataProvider)Activator.CreateInstance(providerType)!;
		var metadata = await provider.GetFunctionMetadataAsync(string.Empty);
		var function = Assert.Single(metadata, item => item.Name == functionName);
		using JsonDocument binding = JsonDocument.Parse(Assert.Single(function.RawBindings!));
		Assert.Equal("queueTrigger", binding.RootElement.GetProperty("type").GetString());
		Assert.Equal("In", binding.RootElement.GetProperty("direction").GetString());
		Assert.Equal("message", binding.RootElement.GetProperty("name").GetString());
		Assert.Equal(queueName, binding.RootElement.GetProperty("queueName").GetString());
		Assert.Equal("AzureWebJobsStorage", binding.RootElement.GetProperty("connection").GetString());
	}
}
