namespace PlanForge.Api.Health;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using PlanForge.Api.Http;
using PlanForge.Infrastructure.Database;

public sealed class HealthFunctions(PlanForgeDbContext database, HttpEndpointRunner endpointRunner)
{
	[Function("Health")]
	public Task<HttpResponseData> HealthAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData request)
		=> endpointRunner.ExecuteAsync(request, () => Task.FromResult<object?>(new { status = "ok", version = PlanForge.Contracts.ProductVersion.Informational }));

	[Function("Readiness")]
	public Task<HttpResponseData> ReadyAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/ready")] HttpRequestData request)
		=> endpointRunner.ExecuteAsync(request, async () =>
		{
			try
			{
				await database.Users.AnyAsync(request.FunctionContext.CancellationToken);
				return new { status = "ready", version = PlanForge.Contracts.ProductVersion.Informational };
			}
			catch (Exception)
			{
				throw new PlanForge.Application.Common.PlanForgeException(503, "The database is not ready. Try again shortly.");
			}
		});
}
