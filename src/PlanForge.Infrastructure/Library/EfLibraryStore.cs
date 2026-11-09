namespace PlanForge.Infrastructure.Library;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Application.Library;
using PlanForge.Contracts.Library;
using PlanForge.Domain.Library;
using PlanForge.Domain.Planning;
using PlanForge.Infrastructure.Database;
using PlanForge.Infrastructure.Planning;

public sealed class EfLibraryStore(PlanForgeDbContext database) : ILibraryStore
{
	public async Task ValidateTargetsAsync(Guid ownerId, Guid? projectId, Guid? phaseId, CancellationToken cancellationToken, Guid? subphaseId = null, Guid? stepId = null)
	{
		var target = ResolveUploadTarget(phaseId, subphaseId, stepId);
		await ResolveTargetsAsync(ownerId, projectId, target.Id, cancellationToken, target.Kind);
	}

	public async Task<int> CleanOrphansAsync(Guid ownerId, IReadOnlyList<Guid> resourceIds, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		int deleted = 0;
		foreach (Guid id in resourceIds.Order())
		{
			// The resource update takes the same row lock as attachment; then zero links are rechecked.
			int locked = await database.Resources.Where(resource => resource.Id == id && resource.OwnerUserId == ownerId
				&& !database.ProjectResources.Any(link => link.ResourceId == id) && !database.PhaseResources.Any(link => link.ResourceId == id))
				.ExecuteUpdateAsync(setters => setters.SetProperty(resource => resource.Revision, resource => resource.Revision), cancellationToken);
			if (locked == 1)
				deleted += await database.Resources.Where(resource => resource.Id == id && resource.OwnerUserId == ownerId
					&& !database.ProjectResources.Any(link => link.ResourceId == id) && !database.PhaseResources.Any(link => link.ResourceId == id)).ExecuteDeleteAsync(cancellationToken);
		}
		await transaction.CommitAsync(cancellationToken);
		return deleted;
	}

	public async Task DeleteAsync(AuthenticatedActor actor, Guid resourceId, long revision, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		foreach (Guid project in await ResolveResourceProjectsAsync(actor.UserId, resourceId, cancellationToken))
			await ProjectEventWriter.TouchAsync(database, actor, project, cancellationToken);
		await LockResourceAsync(actor.UserId, resourceId, revision, cancellationToken);
		await database.ProjectResources.Where(link => link.ResourceId == resourceId).ExecuteDeleteAsync(cancellationToken);
		await database.PhaseResources.Where(link => link.ResourceId == resourceId).ExecuteDeleteAsync(cancellationToken);
		await database.Resources.Where(resource => resource.Id == resourceId && resource.OwnerUserId == actor.UserId).ExecuteDeleteAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
	}

	public Task<Resource?> DownloadAsync(Guid ownerId, Guid resourceId, CancellationToken cancellationToken)
		=> database.Resources.AsNoTracking().SingleOrDefaultAsync(resource => resource.Id == resourceId && resource.OwnerUserId == ownerId, cancellationToken);

	public async Task<ResourceProfile?> GetAsync(Guid ownerId, Guid resourceId, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		return (await LoadProfilesAsync(ownerId, [resourceId], cancellationToken)).SingleOrDefault();
	}

	public async Task<IReadOnlyList<ResourceProfile>> ListAsync(Guid ownerId, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		return await LoadProfilesAsync(ownerId, null, cancellationToken);
	}

	public async Task<IReadOnlyList<ResourceProfile>> ListProjectAsync(Guid ownerId, Guid projectId, Guid? phaseId, CancellationToken cancellationToken, string itemKind = "Phase")
	{
		await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		if (!await database.Projects.AnyAsync(project => project.Id == projectId && project.OwnerUserId == ownerId, cancellationToken))
			throw new PlanForgeException(404, "Project not found.");
		var nodes = await database.WorkItems.AsNoTracking().Where(item => item.ProjectId == projectId && item.Kind != WorkItemKind.Decision)
			.Select(item => new { item.Id, item.ParentId, item.Kind }).ToListAsync(cancellationToken);
		HashSet<Guid> ancestors = [];
		if (phaseId is Guid selectedId)
		{
			var selected = nodes.SingleOrDefault(item => item.Id == selectedId && item.Kind.ToString() == itemKind);
			if (selected is null)
				throw new PlanForgeException(404, "Plan record not found.");
			Guid? current = selectedId;
			Dictionary<Guid, Guid?> parents = nodes.ToDictionary(item => item.Id, item => item.ParentId);
			while (current is Guid id && ancestors.Add(id) && parents.TryGetValue(id, out Guid? parent))
				current = parent;
		}
		List<Guid> direct = await database.ProjectResources.Where(link => link.ProjectId == projectId).Select(link => link.ResourceId).ToListAsync(cancellationToken);
		List<Guid> phases = await (from link in database.PhaseResources
								   join node in database.WorkItems on link.PhaseId equals node.Id
								   where node.ProjectId == projectId && node.Kind != WorkItemKind.Decision && (!phaseId.HasValue || ancestors.Contains(node.Id))
								   select link.ResourceId).ToListAsync(cancellationToken);
		return await LoadProfilesAsync(ownerId, direct.Concat(phases).Distinct().ToList(), cancellationToken);
	}

	public async Task<ResourceUsage> UsageAsync(Guid ownerId, ResourceLimits limits, CancellationToken cancellationToken)
	{
		long own = await database.Resources.Where(resource => resource.OwnerUserId == ownerId).SumAsync(resource => (long?)resource.Size, cancellationToken) ?? 0;
		long global = await database.Resources.SumAsync(resource => (long?)resource.Size, cancellationToken) ?? 0;
		return new ResourceUsage(own, limits.OwnerBudgetBytes, limits.MaximumFileBytes, own * 5 >= limits.OwnerBudgetBytes * 4 || global * 5 >= limits.GlobalBudgetBytes * 4);
	}

	public async Task<ResourceProfile> SetLinkAsync(AuthenticatedActor actor, Guid resourceId, ResourceLinkRequest request, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		await RequireResourceAsync(actor.UserId, resourceId, cancellationToken);
		Guid? itemId = request.PhaseId ?? request.SubphaseId ?? request.StepId;
		WorkItemKind kind = request.SubphaseId.HasValue ? WorkItemKind.Subphase : request.StepId.HasValue ? WorkItemKind.Step : WorkItemKind.Phase;
		HashSet<Guid> projects = await ResolveTargetsAsync(actor.UserId, request.ProjectId, itemId, cancellationToken, kind);
		foreach (Guid project in projects.Order())
			await ProjectEventWriter.TouchAsync(database, actor, project, cancellationToken);
		await LockResourceAsync(actor.UserId, resourceId, request.Revision, cancellationToken);
		if (request.ProjectId is Guid projectId)
		{
			if (request.Attach && !await database.ProjectResources.AnyAsync(link => link.ProjectId == projectId && link.ResourceId == resourceId, cancellationToken))
				database.ProjectResources.Add(new ProjectResource { ProjectId = projectId, ResourceId = resourceId });
			else if (!request.Attach)
				await database.ProjectResources.Where(link => link.ProjectId == projectId && link.ResourceId == resourceId).ExecuteDeleteAsync(cancellationToken);
		}
		if (itemId is Guid phaseId)
		{
			if (request.Attach && !await database.PhaseResources.AnyAsync(link => link.PhaseId == phaseId && link.ResourceId == resourceId, cancellationToken))
				database.PhaseResources.Add(new PhaseResource { PhaseId = phaseId, ResourceId = resourceId });
			else if (!request.Attach)
				await database.PhaseResources.Where(link => link.PhaseId == phaseId && link.ResourceId == resourceId).ExecuteDeleteAsync(cancellationToken);
		}
		await database.SaveChangesAsync(cancellationToken);
		ResourceProfile profile = (await LoadProfilesAsync(actor.UserId, [resourceId], cancellationToken)).Single();
		await transaction.CommitAsync(cancellationToken);
		DetachLinks(resourceId);
		return profile;
	}

	public async Task<ResourceProfile> UpdateAsync(AuthenticatedActor actor, Guid resourceId, ResourceMetadataRequest request, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		foreach (Guid project in await ResolveResourceProjectsAsync(actor.UserId, resourceId, cancellationToken))
			await ProjectEventWriter.TouchAsync(database, actor, project, cancellationToken);
		await LockResourceAsync(actor.UserId, resourceId, request.Revision, cancellationToken);
		await database.Resources.Where(resource => resource.Id == resourceId && resource.OwnerUserId == actor.UserId).ExecuteUpdateAsync(setters => setters.SetProperty(resource => resource.Title, request.Title), cancellationToken);
		ResourceProfile profile = (await LoadProfilesAsync(actor.UserId, [resourceId], cancellationToken)).Single();
		await transaction.CommitAsync(cancellationToken);
		return profile;
	}

	public async Task<ResourceProfile> UploadAsync(AuthenticatedActor actor, Resource resource, Guid? projectId, Guid? phaseId, ResourceLimits limits, CancellationToken cancellationToken, Guid? subphaseId = null, Guid? stepId = null)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		var target = ResolveUploadTarget(phaseId, subphaseId, stepId);
		HashSet<Guid> touchedProjects = await ResolveTargetsAsync(actor.UserId, projectId, target.Id, cancellationToken, target.Kind);
		long ownerBytes = await database.Resources.Where(item => item.OwnerUserId == actor.UserId).SumAsync(item => (long?)item.Size, cancellationToken) ?? 0;
		long globalBytes = await database.Resources.SumAsync(item => (long?)item.Size, cancellationToken) ?? 0;
		if (ownerBytes + resource.Size > limits.OwnerBudgetBytes || globalBytes + resource.Size > limits.GlobalBudgetBytes)
			throw new PlanForgeException(413, "Resource storage is full. Remove unneeded files or contact the owner.");
		if (await database.Resources.CountAsync(item => item.OwnerUserId == actor.UserId, cancellationToken) >= 1000)
			throw new PlanForgeException(413, "The account resource-count limit has been reached.");
		database.Resources.Add(resource);
		if (projectId is Guid project)
			database.ProjectResources.Add(new ProjectResource { ProjectId = project, ResourceId = resource.Id });
		if (target.Id is Guid phase)
			database.PhaseResources.Add(new PhaseResource { PhaseId = phase, ResourceId = resource.Id });
		await database.SaveChangesAsync(cancellationToken);
		foreach (Guid touchedProject in touchedProjects)
			await ProjectEventWriter.TouchAsync(database, actor, touchedProject, cancellationToken);
		ResourceProfile profile = (await LoadProfilesAsync(actor.UserId, [resource.Id], cancellationToken)).Single();
		await transaction.CommitAsync(cancellationToken);
		database.Entry(resource).State = EntityState.Detached;
		foreach (var entry in database.ChangeTracker.Entries<ProjectResource>().Where(entry => entry.Entity.ResourceId == resource.Id).ToList())
			entry.State = EntityState.Detached;
		foreach (var entry in database.ChangeTracker.Entries<PhaseResource>().Where(entry => entry.Entity.ResourceId == resource.Id).ToList())
			entry.State = EntityState.Detached;
		return profile;
	}

	private void DetachLinks(Guid resourceId)
	{
		foreach (var entry in database.ChangeTracker.Entries<ProjectResource>().Where(entry => entry.Entity.ResourceId == resourceId).ToList())
			entry.State = EntityState.Detached;
		foreach (var entry in database.ChangeTracker.Entries<PhaseResource>().Where(entry => entry.Entity.ResourceId == resourceId).ToList())
			entry.State = EntityState.Detached;
	}

	private async Task LockResourceAsync(Guid ownerId, Guid resourceId, long revision, CancellationToken cancellationToken)
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int changed = await database.Resources.Where(resource => resource.Id == resourceId && resource.OwnerUserId == ownerId && resource.Revision == revision)
			.ExecuteUpdateAsync(setters => setters.SetProperty(resource => resource.Revision, resource => resource.Revision + 1).SetProperty(resource => resource.UpdatedAt, now), cancellationToken);
		if (changed != 1)
		{
			await RequireResourceAsync(ownerId, resourceId, cancellationToken);
			throw new PlanForgeException(409, "The resource changed. Reload before retrying.");
		}
	}

	private async Task RequireResourceAsync(Guid ownerId, Guid resourceId, CancellationToken cancellationToken)
	{
		if (!await database.Resources.AnyAsync(resource => resource.Id == resourceId && resource.OwnerUserId == ownerId, cancellationToken))
			throw new PlanForgeException(404, "Resource not found.");
	}

	private async Task<List<Guid>> ResolveResourceProjectsAsync(Guid ownerId, Guid resourceId, CancellationToken cancellationToken)
	{
		await RequireResourceAsync(ownerId, resourceId, cancellationToken);
		List<Guid> direct = await (from link in database.ProjectResources
								   join project in database.Projects on link.ProjectId equals project.Id
								   where link.ResourceId == resourceId && project.OwnerUserId == ownerId
								   select project.Id).ToListAsync(cancellationToken);
		List<Guid> phases = await (from link in database.PhaseResources
								   join phase in database.WorkItems on link.PhaseId equals phase.Id
								   join project in database.Projects on phase.ProjectId equals project.Id
								   where link.ResourceId == resourceId && project.OwnerUserId == ownerId
								   select project.Id).ToListAsync(cancellationToken);
		return direct.Concat(phases).Distinct().Order().ToList();
	}

	private async Task<IReadOnlyList<ResourceProfile>> LoadProfilesAsync(Guid ownerId, List<Guid>? resourceIds, CancellationToken cancellationToken)
	{
		// Deliberately project metadata only; listing never materializes the binary Content column.
		List<Resource> resources = await database.Resources.AsNoTracking().Where(resource => resource.OwnerUserId == ownerId && (resourceIds == null || resourceIds.Contains(resource.Id)))
			.OrderBy(resource => resource.Title).Select(resource => new Resource { Id = resource.Id, Title = resource.Title, FileName = resource.FileName, ContentType = resource.ContentType, Size = resource.Size, Sha256 = resource.Sha256, Revision = resource.Revision, CreatedAt = resource.CreatedAt, UpdatedAt = resource.UpdatedAt }).ToListAsync(cancellationToken);
		List<Guid> ids = resources.Select(resource => resource.Id).ToList();
		var projectLinks = await (from link in database.ProjectResources
								  join project in database.Projects on link.ProjectId equals project.Id
								  where ids.Contains(link.ResourceId) && project.OwnerUserId == ownerId
								  select new { link.ResourceId, Association = new ProjectResourceAssociation(project.Id, project.Name) }).ToListAsync(cancellationToken);
		var phaseLinks = await (from link in database.PhaseResources
								join phase in database.WorkItems on link.PhaseId equals phase.Id
								join project in database.Projects on phase.ProjectId equals project.Id
								where ids.Contains(link.ResourceId) && project.OwnerUserId == ownerId && phase.Kind == WorkItemKind.Phase
								select new { link.ResourceId, Association = new PhaseResourceAssociation(phase.Id, phase.Title, project.Id, project.Name) }).ToListAsync(cancellationToken);
		var itemLinks = await (from link in database.PhaseResources
							   join item in database.WorkItems on link.PhaseId equals item.Id
							   join project in database.Projects on item.ProjectId equals project.Id
							   where ids.Contains(link.ResourceId) && project.OwnerUserId == ownerId && item.Kind != WorkItemKind.Decision
							   select new { link.ResourceId, Association = new PlanItemResourceAssociation(item.Id, item.Title, item.Kind.ToString(), project.Id, project.Name) }).ToListAsync(cancellationToken);
		return resources.Select(resource => new ResourceProfile(resource.Id, resource.Title, resource.FileName, resource.ContentType, resource.Size, resource.Sha256, resource.Revision, resource.CreatedAt, resource.UpdatedAt, projectLinks.Where(link => link.ResourceId == resource.Id).Select(link => link.Association).ToList(), phaseLinks.Where(link => link.ResourceId == resource.Id).Select(link => link.Association).ToList()) { Items = itemLinks.Where(link => link.ResourceId == resource.Id).Select(link => link.Association).ToList() }).ToList();
	}

	private static (Guid? Id, WorkItemKind Kind) ResolveUploadTarget(Guid? phaseId, Guid? subphaseId, Guid? stepId)
	{
		if (new[] { phaseId, subphaseId, stepId }.Count(id => id.HasValue) > 1)
			throw new PlanForgeException(400, "Choose at most one structural resource target.");
		return (phaseId ?? subphaseId ?? stepId, subphaseId.HasValue ? WorkItemKind.Subphase : stepId.HasValue ? WorkItemKind.Step : WorkItemKind.Phase);
	}

	private async Task<HashSet<Guid>> ResolveTargetsAsync(Guid ownerId, Guid? projectId, Guid? phaseId, CancellationToken cancellationToken, WorkItemKind kind = WorkItemKind.Phase)
	{
		HashSet<Guid> projects = [];
		if (projectId is Guid project)
		{
			if (!await database.Projects.AnyAsync(item => item.Id == project && item.OwnerUserId == ownerId, cancellationToken))
				throw new PlanForgeException(404, "Project not found.");
			projects.Add(project);
		}
		if (phaseId is Guid phase)
		{
			Guid? parentProject = await (from item in database.WorkItems
										 join parent in database.Projects on item.ProjectId equals parent.Id
										 where item.Id == phase && item.Kind == kind && parent.OwnerUserId == ownerId
										 select (Guid?)parent.Id).SingleOrDefaultAsync(cancellationToken);
			if (parentProject is null || projectId.HasValue && parentProject != projectId)
				throw new PlanForgeException(404, "Plan record not found.");
			projects.Add(parentProject.Value);
		}
		return projects;
	}
}
