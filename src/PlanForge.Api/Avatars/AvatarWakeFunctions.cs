namespace PlanForge.Api.Avatars;

using Microsoft.Azure.Functions.Worker;
using PlanForge.Infrastructure.Accounts;

public sealed class AvatarWakeFunctions(AvatarWorker worker)
{
	[Function("AvatarWake")]
	public Task WakeAsync([QueueTrigger(QueueAvatarWakeSignal.QueueName, Connection = "AzureWebJobsStorage")] string message, FunctionContext context)
		=> worker.DrainQueuedAsync(context.CancellationToken);

	[Function("AvatarWakePoison")]
	public Task RecoverAsync([QueueTrigger(QueueAvatarWakeSignal.QueueName + "-poison", Connection = "AzureWebJobsStorage")] string message, FunctionContext context)
		=> worker.DrainQueuedAsync(context.CancellationToken);
}
