namespace PlanForge.Api.Tests;

using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using PlanForge.Api.Accounts;

public sealed class GeneratedMetadataTests
{
	[Fact]
	public async Task WorkerIndexing_WhenCompiled_ContainsAllHttpFunctionsAsync()
	{
		Type providerType = Assert.Single(typeof(AccountFunctions).Assembly.GetTypes(), type => typeof(IFunctionMetadataProvider).IsAssignableFrom(type));
		var provider = (IFunctionMetadataProvider)Activator.CreateInstance(providerType)!;

		var metadata = await provider.GetFunctionMetadataAsync(string.Empty);

		string[] names = metadata.Select(item => item.Name!).Order().ToArray();
		Assert.Equal(new[] { "Health", "Login", "Logout", "Readiness", "Register", "Session" }, names);
		Assert.All(metadata, item => Assert.NotEmpty(item.RawBindings!));
	}
}
