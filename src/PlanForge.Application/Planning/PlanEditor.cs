namespace PlanForge.Application.Planning;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Domain.Planning;
using PlanForge.Contracts.Planning;

public sealed class PlanEditor(ProjectService projects)
{
	public async Task<ProjectSnapshot> DeleteItemAsync(AuthenticatedActor actor, Guid projectId, string kind, Guid itemId, ConfirmedRevisionRequest command, CancellationToken cancellationToken = default)
	{
		ProjectSnapshot snapshot = await projects.GetAsync(actor, projectId, cancellationToken);
		if (!snapshot.Items.Any(item => item.Id == itemId && item.Kind == kind))
			throw new PlanForgeException(404, "Plan record not found.");
		return await RemoveAsync(actor, snapshot, [itemId], command, cancellationToken);
	}
	public async Task<ProjectSnapshot> CleanCompletedAsync(AuthenticatedActor actor, Guid projectId, ConfirmedRevisionRequest command, CancellationToken cancellationToken = default)
	{
		ProjectSnapshot snapshot = await projects.GetAsync(actor, projectId, cancellationToken);
		snapshot = PlanProgress.Recalculate(snapshot);
		return await RemoveAsync(actor, snapshot, snapshot.Items.Where(item => item.Kind == "Phase" && item.Status == "Completed").Select(item => item.Id).ToHashSet(), command, cancellationToken);
	}

	public async Task<ProjectSnapshot> SaveItemAsync(AuthenticatedActor actor, Guid projectId, string kind, Guid? itemId, PlanItemCommand command, CancellationToken cancellationToken = default)
	{
		ProjectSnapshot snapshot = await projects.GetAsync(actor, projectId, cancellationToken);
		if (snapshot.Revision != command.Revision)
			throw new PlanForgeException(409, "The project changed. Reload before editing.");
		PlanSnapshotValidator.ParseEnum<WorkItemKind>(kind);
		PlanItem? previous = itemId is null ? null : snapshot.Items.SingleOrDefault(item => item.Id == itemId && item.Kind == kind) ?? throw new PlanForgeException(404, "Plan record not found.");
		Guid id = itemId ?? Guid.NewGuid();
		List<PlanItem> items = snapshot.Items.Where(item => item.Id != id).ToList();
		List<PlanItem> siblings = items.Where(item => item.ParentId == command.ParentId && item.Kind == kind).OrderBy(item => item.SortOrder).ToList();
		int position = command.SortOrder ?? (previous is not null && previous.ParentId == command.ParentId ? previous.SortOrder : siblings.Count);
		if (position < 0 || position > siblings.Count)
			throw new PlanForgeException(400, "The requested position is outside the sibling list.");
		var edited = new PlanItem(id, kind, command.ParentId, position, command.Title, command.Description, command.Status, command.AssignmentMode, command.AssignedAgentId, command.Resolution, command.Evidence);
		siblings.Insert(position, edited);
		items.RemoveAll(item => item.ParentId == command.ParentId && item.Kind == kind);
		items.AddRange(siblings.Select((item, index) => item with { SortOrder = index }));
		if (itemId is null && kind == "Phase")
		{
			var phase = new Phase(command.Title);
			items.AddRange(phase.Subphases.Select((subphase, index) => new PlanItem(Guid.NewGuid(), "Subphase", id, index, subphase.Title)));
		}
		return await projects.SaveAsync(actor, projectId, snapshot with { Items = items }, false, cancellationToken);
	}
	private async Task<ProjectSnapshot> RemoveAsync(AuthenticatedActor actor, ProjectSnapshot snapshot, HashSet<Guid> removed, ConfirmedRevisionRequest command, CancellationToken cancellationToken)
	{
		if (!command.Confirmed)
			throw new PlanForgeException(400, "Confirm permanent deletion of the selected work and its descendants.");
		if (snapshot.Revision != command.Revision)
			throw new PlanForgeException(409, "The project changed. Reload before deleting work.");
		int previousCount;
		do
		{
			previousCount = removed.Count;
			foreach (PlanItem item in snapshot.Items.Where(item => item.ParentId.HasValue && removed.Contains(item.ParentId.Value)))
				removed.Add(item.Id);
		} while (previousCount != removed.Count);
		return await projects.SaveAsync(actor, snapshot.Id, snapshot with { Items = snapshot.Items.Where(item => !removed.Contains(item.Id)).ToList() }, true, cancellationToken);
	}
}
