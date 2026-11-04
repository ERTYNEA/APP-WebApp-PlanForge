namespace PlanForge.Api.Settings;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using PlanForge.Api.Http;
using PlanForge.Application.Accounts;
using PlanForge.Application.Library;
using PlanForge.Contracts;
using PlanForge.Domain.Planning;

public sealed class SettingsFunctions(AccountService accounts, ResourceLimits limits, HttpEndpointRunner runner)
{
	[Function("Settings")]
	public Task<HttpResponseData> SettingsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/settings")] HttpRequestData request)
		=> runner.ExecuteAsync(request, async () =>
		{
			AuthenticatedActor actor = await RequestActorReader.ReadAsync(request, accounts);
			runner.AcquireBudget("api:" + actor.UserId, 600, TimeSpan.FromMinutes(1));
			return new ProductSettings(ProductVersion.Informational, AvatarValidator.MaximumBytes, limits.MaximumFileBytes, limits.OwnerBudgetBytes, limits.GlobalBudgetBytes, Enum.GetNames<DevelopmentWorkflow>());
		});
}
