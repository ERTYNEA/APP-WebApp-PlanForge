namespace PlanForge.Application.Exchange;

using PlanForge.Application.Accounts;
using PlanForge.Application.Planning;
using PlanForge.Contracts.Planning;

public sealed class WorkPlanExchange(ProjectService projects, IProjectStore store)
{
	public async Task<byte[]> ExportAsync(AuthenticatedActor actor, Guid projectId, CancellationToken cancellationToken = default)
		=> (await ExportWithReceiptAsync(actor, projectId, cancellationToken)).Content;

	public async Task<PlanForge.Contracts.DownloadFile> ExportWithReceiptAsync(AuthenticatedActor actor, Guid projectId, CancellationToken cancellationToken = default)
	{
		ProjectSnapshot snapshot = await projects.GetAsync(actor, projectId, cancellationToken);
		byte[] payload = WorkPlanCsvCodec.Serialize(snapshot);
		long changeId = await store.RecordEventAsync(actor.UserId, projectId, snapshot.Revision, actor.AgentId ?? actor.UserId, actor.IsHuman ? "Human" : "Agent", actor.IsHuman ? "Project exported successfully" : "Project exported successfully by agent", cancellationToken, actor.AgentNameSnapshot);
		return new(payload, "text/csv; charset=utf-8", "work-plan.csv") { ProjectChangeId = changeId };
	}

	public async Task<ProjectSnapshot> ImportAsync(AuthenticatedActor actor, Guid projectId, byte[] payload, bool replace = false, CancellationToken cancellationToken = default)
	{
		// Ownership precedes parsing, so malformed content cannot probe another user's project.
		await projects.GetAsync(actor, projectId, cancellationToken);
		ProjectSnapshot snapshot = WorkPlanCsvCodec.Parse(payload);
		return await projects.SaveAsync(actor, projectId, snapshot, replace, cancellationToken, actor.IsHuman ? "CSV imported successfully" : "CSV imported successfully by agent");
	}
}
