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

		string[] names = metadata.Select(item => item.Name!).Order(StringComparer.Ordinal).ToArray();
		Assert.Equal(new[] { "Agent", "AgentAvatar", "AgentTokens", "Agents", "ChangePassword", "CleanAgentTokens", "CleanCompletedPhases", "CleanOrphanResources", "Decisions", "DownloadResource", "ExportProjectArchive", "ExportWorkPlan", "Health", "ImportWorkPlan", "Login", "Logout", "PhaseResources", "Phases", "Project", "ProjectChanges", "ProjectResources", "ProjectSummaries", "Projects", "Readiness", "Register", "RenewAgentToken", "Resource", "ResourceLinks", "ResourceUsage", "Resources", "RevokeAgentToken", "Session", "Settings", "Steps", "Subphases", "UserAvatar", "UserProfile" }.Order(StringComparer.Ordinal), names);
		Assert.All(metadata, item => Assert.NotEmpty(item.RawBindings!));
	}
}
