namespace PlanForge.Infrastructure.Planning;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Common;
using PlanForge.Application.Planning;
using PlanForge.Domain.Planning;
using PlanForge.Infrastructure.Database;

public sealed class EfProjectStore(PlanForgeDbContext database) : IProjectStore
{
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
		await RemoveItemsAsync(projectId, [], cancellationToken);
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
	public async Task SaveAsync(ProjectAggregate aggregate, long expectedRevision, Guid actorId, string actorKind, string action, CancellationToken cancellationToken)
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
		var change = new ProjectChange { ProjectId = project.Id, Revision = expectedRevision + 1, ActorId = actorId, ActorKind = actorKind, Action = action, CreatedAt = DateTime.UtcNow };
		database.ProjectChanges.Add(change);
		try
		{
			await database.SaveChangesAsync(cancellationToken);
			await RemoveItemsAsync(project.Id, aggregate.Items.Select(item => item.Id).ToList(), cancellationToken);
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
	public async Task RecordEventAsync(Guid ownerId, Guid projectId, long revision, Guid actorId, string actorKind, string action, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		if (!await database.Projects.AnyAsync(project => project.Id == projectId && project.OwnerUserId == ownerId, cancellationToken))
			throw new PlanForgeException(404, "Project not found.");
		var change = new ProjectChange { ProjectId = projectId, Revision = revision, ActorId = actorId, ActorKind = actorKind, Action = action, CreatedAt = DateTime.UtcNow };
		database.ProjectChanges.Add(change);
		await database.SaveChangesAsync(cancellationToken);
		List<long> expired = await database.ProjectChanges.Where(entry => entry.ProjectId == projectId).OrderByDescending(entry => entry.Id).Skip(100).Select(entry => entry.Id).ToListAsync(cancellationToken);
		await database.ProjectChanges.Where(entry => expired.Contains(entry.Id)).ExecuteDeleteAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		database.Entry(change).State = EntityState.Detached;
	}

	private async Task RemoveItemsAsync(Guid projectId, List<Guid> retained, CancellationToken cancellationToken)
	{
		foreach (WorkItemKind kind in new[] { WorkItemKind.Decision, WorkItemKind.Step, WorkItemKind.Subphase, WorkItemKind.Phase })
			await database.WorkItems.Where(item => item.ProjectId == projectId && !retained.Contains(item.Id) && item.Kind == kind).ExecuteDeleteAsync(cancellationToken);
	}
}
