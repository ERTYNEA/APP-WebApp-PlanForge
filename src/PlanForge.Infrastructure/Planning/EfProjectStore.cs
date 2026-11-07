namespace PlanForge.Infrastructure.Planning;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Domain.Planning;
using PlanForge.Contracts.Planning;
using PlanForge.Infrastructure.Database;

public sealed class EfProjectStore(PlanForgeDbContext database) : IProjectStore
{
	public async Task<ProjectSummaryPage> ListSummariesAsync(Guid ownerId, int skip, int take, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		int total = await database.Projects.CountAsync(project => project.OwnerUserId == ownerId, cancellationToken);
		var records = await database.Projects.AsNoTracking().Where(project => project.OwnerUserId == ownerId).OrderByDescending(project => project.UpdatedAt).ThenBy(project => project.Id).Skip(skip).Take(take)
			.Select(project => new
			{
				project.Id,
				project.Revision,
				project.Name,
				project.Status,
				project.Workflow,
				project.UpdatedAt,
				Description = project.Description.Length > 240 ? project.Description.Substring(0, 240) : project.Description,
				PhaseCount = database.WorkItems.Count(item => item.ProjectId == project.Id && item.Kind == WorkItemKind.Phase),
				StepCount = database.WorkItems.Count(item => item.ProjectId == project.Id && item.Kind == WorkItemKind.Step),
				Completed = database.WorkItems.Count(item => item.ProjectId == project.Id && item.Kind == WorkItemKind.Step && item.Status == WorkStatus.Completed),
				Decisions = database.WorkItems.Count(item => item.ProjectId == project.Id && item.Kind == WorkItemKind.Decision && item.Status != WorkStatus.Completed),
				Latest = database.ProjectChanges.Where(change => change.ProjectId == project.Id).Select(change => (long?)change.Id).Max() ?? 0
			}).ToListAsync(cancellationToken);
		List<ProjectSummary> summaries = records.Select(project =>
		{
			int progress = project.StepCount == 0 ? 0 : project.Completed * 100 / project.StepCount;
			if (progress == 100 && project.Decisions > 0)
				progress = 99;
			return new ProjectSummary(project.Id, project.Revision, project.Name, project.Description, project.Status.ToString(), project.Workflow.ToString(), new DateTimeOffset(DateTime.SpecifyKind(project.UpdatedAt, DateTimeKind.Utc)), project.PhaseCount, project.StepCount, project.Completed, project.Decisions, progress, project.Latest);
		}).ToList();
		return new ProjectSummaryPage(summaries, total, skip, take, skip + summaries.Count < total);
	}

	public async Task<ProjectChangePage?> GetChangesAsync(Guid ownerId, Guid projectId, long after, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		long? revision = await database.Projects.Where(project => project.Id == projectId && project.OwnerUserId == ownerId).Select(project => (long?)project.Revision).SingleOrDefaultAsync(cancellationToken);
		if (!revision.HasValue)
			return null;
		string deletedAgentSuffix = ApplicationText.Get("DeletedAgentSuffix");
		var records = await database.ProjectChanges.AsNoTracking().Where(change => change.ProjectId == projectId).OrderByDescending(change => change.Id).Take(100)
			.Select(change => new
			{
				change.Id,
				change.Revision,
				change.ActorId,
				change.ActorKind,
				change.Action,
				change.CreatedAt,
				ActorName = change.ActorKind == "Human" ? database.Users.Where(user => user.Id == change.ActorId && user.Id == ownerId).Select(user => user.DisplayName).FirstOrDefault()
					: database.Agents.Where(agent => agent.Id == change.ActorId && agent.OwnerUserId == ownerId).Select(agent => agent.Name).FirstOrDefault() ?? (change.ActorNameSnapshot == null ? null : change.ActorNameSnapshot + deletedAgentSuffix)
			}).ToListAsync(cancellationToken);
		long latest = records.Count == 0 ? 0 : records[0].Id;
		bool reset = after > latest || after > 0 && records.Count == 100 && after < records[^1].Id;
		return new ProjectChangePage(revision.Value, latest, reset, records.Where(change => change.Id > after).OrderBy(change => change.Id)
			.Select(change => new ProjectChangeInfo(change.Id, change.Revision, change.ActorId, change.ActorKind, change.ActorName ?? ApplicationText.Get(change.ActorKind == "Human" ? "AnonymousHuman" : "AnonymousAgent"), change.Action, new DateTimeOffset(DateTime.SpecifyKind(change.CreatedAt, DateTimeKind.Utc)))).ToList());
	}

	public Task<bool> IsValidAgentAsync(Guid ownerId, Guid agentId, bool requireActive, CancellationToken cancellationToken)
		=> database.Agents.AnyAsync(agent => agent.OwnerUserId == ownerId && agent.Id == agentId && (!requireActive || agent.IsActive), cancellationToken);

	public async Task CreateAsync(Project project, CancellationToken cancellationToken)
	{
		database.Projects.Add(project);
		await database.SaveChangesAsync(cancellationToken);
		database.Entry(project).State = EntityState.Detached;
	}

	public async Task DeleteAsync(Guid ownerId, Guid projectId, long expectedRevision, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		int locked = await database.Projects.Where(project => project.Id == projectId && project.OwnerUserId == ownerId && project.Revision == expectedRevision).ExecuteUpdateAsync(setters => setters.SetProperty(project => project.Revision, expectedRevision + 1), cancellationToken);
		if (locked != 1)
			throw new PlanForgeException(409, "The project changed. Reload before deleting.");
		List<Guid> directResources = await database.ProjectResources.Where(link => link.ProjectId == projectId).Select(link => link.ResourceId).ToListAsync(cancellationToken);
		await TouchResourcesAsync(ownerId, directResources, cancellationToken);
		await database.ProjectResources.Where(link => link.ProjectId == projectId).ExecuteDeleteAsync(cancellationToken);
		await RemoveItemsAsync(ownerId, projectId, [], cancellationToken);
		await database.ProjectChanges.Where(change => change.ProjectId == projectId).ExecuteDeleteAsync(cancellationToken);
		await database.Projects.Where(project => project.Id == projectId && project.OwnerUserId == ownerId).ExecuteDeleteAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
	}
	public async Task<ProjectAggregate?> GetAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		Project? project = await database.Projects.AsNoTracking().SingleOrDefaultAsync(project => project.OwnerUserId == ownerId && project.Id == projectId, cancellationToken);
		return project is null ? null : new ProjectAggregate(project, await database.WorkItems.AsNoTracking().Where(item => item.ProjectId == projectId).OrderBy(item => item.SortOrder).ThenBy(item => item.Id).ToListAsync(cancellationToken));
	}
	public async Task<IReadOnlyList<ProjectAggregate>> ListAsync(Guid ownerId, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		List<Project> projects = await database.Projects.AsNoTracking().Where(project => project.OwnerUserId == ownerId).OrderByDescending(project => project.UpdatedAt).Take(200).ToListAsync(cancellationToken);
		List<Guid> ids = projects.Select(project => project.Id).ToList();
		List<WorkItem> items = await database.WorkItems.AsNoTracking().Where(item => ids.Contains(item.ProjectId)).ToListAsync(cancellationToken);
		return projects.Select(project => new ProjectAggregate(project, items.Where(item => item.ProjectId == project.Id).ToList())).ToList();
	}
	public async Task SaveAsync(ProjectAggregate aggregate, long expectedRevision, Guid actorId, string actorKind, string action, CancellationToken cancellationToken, string? actorNameSnapshot = null)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		Project project = aggregate.Project;
		int changed = await database.Projects.Where(current => current.Id == project.Id && current.OwnerUserId == project.OwnerUserId && current.Revision == expectedRevision)
			.ExecuteUpdateAsync(setters => setters.SetProperty(current => current.Revision, expectedRevision + 1)
				.SetProperty(current => current.Name, project.Name).SetProperty(current => current.Description, project.Description)
				.SetProperty(current => current.Workflow, project.Workflow).SetProperty(current => current.Status, project.Status)
				.SetProperty(current => current.UpdatedAt, project.UpdatedAt), cancellationToken);
		if (changed != 1)
			throw new PlanForgeException(409, "The project changed. Reload before retrying.");
		List<Guid> existing = await database.WorkItems.Where(item => item.ProjectId == project.Id).Select(item => item.Id).ToListAsync(cancellationToken);
		foreach (WorkItem item in aggregate.Items)
		{
			if (existing.Contains(item.Id))
				database.WorkItems.Update(item);
			else
				database.WorkItems.Add(item);
		}
		var change = new ProjectChange { ProjectId = project.Id, Revision = expectedRevision + 1, ActorId = actorId, ActorKind = actorKind, ActorNameSnapshot = actorNameSnapshot, Action = action, CreatedAt = DateTime.UtcNow };
		database.ProjectChanges.Add(change);
		try
		{
			await database.SaveChangesAsync(cancellationToken);
			await RemoveItemsAsync(project.OwnerUserId, project.Id, aggregate.Items.Select(item => item.Id).ToList(), cancellationToken);
			List<long> expired = await database.ProjectChanges.Where(entry => entry.ProjectId == project.Id).OrderByDescending(entry => entry.Id).Skip(100).Select(entry => entry.Id).ToListAsync(cancellationToken);
			await database.ProjectChanges.Where(entry => expired.Contains(entry.Id)).ExecuteDeleteAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
		}
		catch (DbUpdateException)
		{
			throw new PlanForgeException(409, "The plan could not be saved because a record or relationship conflicts. Reload and reconcile the plan.");
		}
		finally
		{
			database.Entry(change).State = EntityState.Detached;
			foreach (WorkItem item in aggregate.Items)
				database.Entry(item).State = EntityState.Detached;
		}
	}
	public async Task RecordEventAsync(Guid ownerId, Guid projectId, long revision, Guid actorId, string actorKind, string action, CancellationToken cancellationToken, string? actorNameSnapshot = null)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		if (!await database.Projects.AnyAsync(project => project.Id == projectId && project.OwnerUserId == ownerId, cancellationToken))
			throw new PlanForgeException(404, "Project not found.");
		var change = new ProjectChange { ProjectId = projectId, Revision = revision, ActorId = actorId, ActorKind = actorKind, ActorNameSnapshot = actorNameSnapshot, Action = action, CreatedAt = DateTime.UtcNow };
		database.ProjectChanges.Add(change);
		await database.SaveChangesAsync(cancellationToken);
		List<long> expired = await database.ProjectChanges.Where(entry => entry.ProjectId == projectId).OrderByDescending(entry => entry.Id).Skip(100).Select(entry => entry.Id).ToListAsync(cancellationToken);
		await database.ProjectChanges.Where(entry => expired.Contains(entry.Id)).ExecuteDeleteAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		database.Entry(change).State = EntityState.Detached;
	}

	private async Task RemoveItemsAsync(Guid ownerId, Guid projectId, List<Guid> retained, CancellationToken cancellationToken)
	{
		List<Guid> removedPhases = await database.WorkItems.Where(item => item.ProjectId == projectId && !retained.Contains(item.Id) && item.Kind == WorkItemKind.Phase).Select(item => item.Id).ToListAsync(cancellationToken);
		List<Guid> resources = await database.PhaseResources.Where(link => removedPhases.Contains(link.PhaseId)).Select(link => link.ResourceId).Distinct().ToListAsync(cancellationToken);
		await TouchResourcesAsync(ownerId, resources, cancellationToken);
		await database.PhaseResources.Where(link => removedPhases.Contains(link.PhaseId)).ExecuteDeleteAsync(cancellationToken);
		foreach (WorkItemKind kind in new[] { WorkItemKind.Decision, WorkItemKind.Step, WorkItemKind.Subphase, WorkItemKind.Phase })
			await database.WorkItems.Where(item => item.ProjectId == projectId && !retained.Contains(item.Id) && item.Kind == kind).ExecuteDeleteAsync(cancellationToken);
	}
	private async Task TouchResourcesAsync(Guid ownerId, List<Guid> ids, CancellationToken cancellationToken)
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		foreach (Guid id in ids.Order())
			await database.Resources.Where(resource => resource.Id == id && resource.OwnerUserId == ownerId).ExecuteUpdateAsync(setters => setters.SetProperty(resource => resource.Revision, resource => resource.Revision + 1).SetProperty(resource => resource.UpdatedAt, now), cancellationToken);
	}
}
