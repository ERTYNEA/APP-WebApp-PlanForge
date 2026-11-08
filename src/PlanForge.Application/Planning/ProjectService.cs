namespace PlanForge.Application.Planning;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Contracts.Planning;
using PlanForge.Domain.Planning;

public sealed class ProjectService(IProjectStore store, TimeProvider timeProvider)
{
	public Task<WorkspaceDashboard> GetDashboardAsync(AuthenticatedActor actor, CancellationToken cancellationToken = default)
		=> store.GetDashboardAsync(actor.UserId, cancellationToken);
	public async Task<ProjectSnapshot> CreateAsync(AuthenticatedActor actor, CreateProjectRequest request, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 160 || request.Description is null || request.Description.Length > 8000 || !Enum.TryParse(request.Workflow, out DevelopmentWorkflow workflow) || !Enum.IsDefined(workflow))
			throw new PlanForgeException(400, "Provide a project name, description and valid development workflow.");
		DateTimeOffset now = timeProvider.GetUtcNow();
		var project = new Project { Name = request.Name.Trim(), Description = request.Description, Workflow = workflow, OwnerUserId = actor.UserId, CreatedAt = now, UpdatedAt = now.UtcDateTime };
		await store.CreateAsync(project, cancellationToken);
		return ToSnapshot(new ProjectAggregate(project, []));
	}

	public Task<ProjectSummaryPage> ListSummariesAsync(AuthenticatedActor actor, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
	{
		if (skip is < 0 or > 100000 || take is < 1 or > 100)
			throw new PlanForgeException(400, "Use skip 0–100000 and take 1–100.");
		return store.ListSummariesAsync(actor.UserId, skip, take, cancellationToken);
	}

	public async Task<ProjectChangePage> GetChangesAsync(AuthenticatedActor actor, Guid projectId, long after = 0, CancellationToken cancellationToken = default)
	{
		if (after < 0)
			throw new PlanForgeException(400, "Use a nonnegative change cursor.");
		return await store.GetChangesAsync(actor.UserId, projectId, after, cancellationToken) ?? throw new PlanForgeException(404, "Project not found.");
	}

	public async Task<ProjectSnapshot> GetAsync(AuthenticatedActor actor, Guid projectId, CancellationToken cancellationToken = default)
		=> ToSnapshot(await store.GetAsync(actor.UserId, projectId, cancellationToken) ?? throw new PlanForgeException(404, "Project not found."));
	public async Task<IReadOnlyList<ProjectSnapshot>> ListAsync(AuthenticatedActor actor, CancellationToken cancellationToken = default)
		=> (await store.ListAsync(actor.UserId, cancellationToken)).Select(ToSnapshot).ToList();

	public async Task<ProjectSnapshot> SaveAsync(AuthenticatedActor actor, Guid projectId, ProjectSnapshot snapshot, bool replace = false, CancellationToken cancellationToken = default, string action = "Project updated")
	{
		ProjectAggregate aggregate = await store.GetAsync(actor.UserId, projectId, cancellationToken) ?? throw new PlanForgeException(404, "Project not found.");
		if (snapshot.Id != projectId)
			throw new PlanForgeException(400, "Project ID must match the target.");
		if (aggregate.Project.Revision != snapshot.Revision)
			throw new PlanForgeException(409, "The project changed. Reload the current revision before retrying.");
		PlanSnapshotValidator.Validate(snapshot);
		snapshot = PlanProgress.Recalculate(snapshot with
		{
			Items = snapshot.Items.Select(item => item.Kind == "Step" && item.Status == "Pending" && item.Evidence == "WorkDone" ? item with { Status = "InProgress" } : item).ToList()
		});
		if (!replace && aggregate.Items.Any(existing => !snapshot.Items.Any(item => item.Id == existing.Id)))
			throw new PlanForgeException(400, "Records are missing. Explicit replace confirmation is required to remove plan records.");
		foreach (Guid agentId in snapshot.Items.Where(item => item.AssignedAgentId.HasValue).Select(item => item.AssignedAgentId!.Value).Distinct())
			if (!await store.IsValidAgentAsync(actor.UserId, agentId, !aggregate.Items.Any(item => item.AssignedAgentId == agentId), cancellationToken))
				throw new PlanForgeException(400, "An assigned agent is unavailable in this account.");
		DateTimeOffset now = timeProvider.GetUtcNow();
		DevelopmentWorkflow workflow = PlanSnapshotValidator.ParseEnum<DevelopmentWorkflow>(snapshot.Workflow);
		if (workflow != aggregate.Project.Workflow && (!actor.IsHuman || snapshot.Items.Any(item => item.Kind == "Step" && item.Status == "Completed" && !aggregate.Items.Any(old => old.Id == item.Id && old.Status == WorkStatus.Completed))))
			throw new PlanForgeException(400, "Only the owner may change workflow, separately from completing work.");
		aggregate.Project.Name = snapshot.Name;
		aggregate.Project.Description = snapshot.Description;
		aggregate.Project.Status = Enum.Parse<WorkStatus>(snapshot.Status);
		aggregate.Project.Workflow = Enum.Parse<DevelopmentWorkflow>(snapshot.Workflow);
		aggregate.Project.UpdatedAt = now.UtcDateTime;
		List<WorkItem> items = snapshot.Items.Select(item => new WorkItem
		{
			Id = item.Id,
			ProjectId = projectId,
			Kind = Enum.Parse<WorkItemKind>(item.Kind),
			ParentId = item.ParentId,
			SortOrder = item.SortOrder,
			Title = item.Title,
			Description = item.Description,
			Status = item.Kind == "Decision" && item.Status == "Decided" ? WorkStatus.Completed : Enum.Parse<WorkStatus>(item.Status),
			AssignmentMode = Enum.Parse<AssignmentMode>(item.AssignmentMode),
			AssignedAgentId = item.AssignedAgentId,
			Resolution = item.Resolution,
			UpdatedAt = now
		}).ToList();
		foreach (WorkItem item in items)
		{
			PlanItem input = snapshot.Items.Single(candidate => candidate.Id == item.Id);
			WorkItem? previous = aggregate.Items.SingleOrDefault(candidate => candidate.Id == item.Id);
			if (previous is not null && previous.Kind != item.Kind)
				throw new PlanForgeException(400, "A record type cannot change. Create a new record instead.");
			if (item.Status == WorkStatus.Completed && item.Kind == WorkItemKind.Step)
			{
				if (previous?.Status == WorkStatus.Completed)
				{
					item.Evidence = previous.Evidence;
					item.CompletedAt = previous.CompletedAt;
					item.CompletedBy = previous.CompletedBy;
				}
				else
				{
					if (snapshot.Items.Any(decision => decision.Kind == "Decision" && decision.ParentId == item.Id && decision.Status == "Pending"))
						throw new PlanForgeException(400, "Resolve pending decisions on this step before completion.");
					CompletionEvidence evidence = PlanSnapshotValidator.ParseEnum<CompletionEvidence>(input.Evidence ?? string.Empty);
					if (!CompletionPolicy.CanComplete(workflow, evidence, actor.IsHuman))
						throw new PlanForgeException(400, "This workflow requires owner approval, confirmed merge or confirmed push before completion.");
					item.Evidence = evidence;
					item.CompletedAt = now;
					item.CompletedBy = actor.AgentId ?? actor.UserId;
				}
			}
			else if (input.Evidence is not null)
			{
				item.Evidence = PlanSnapshotValidator.ParseEnum<CompletionEvidence>(input.Evidence);
				if (item.Evidence != CompletionEvidence.WorkDone)
					throw new PlanForgeException(400, "Closure evidence is recorded only by a valid completion transition.");
			}
			if (item.Kind is WorkItemKind.Decision or WorkItemKind.Phase or WorkItemKind.Subphase && item.Status == WorkStatus.Completed)
			{
				item.CompletedAt = previous?.Status == WorkStatus.Completed ? previous.CompletedAt : now;
				item.CompletedBy = previous?.Status == WorkStatus.Completed ? previous.CompletedBy : actor.AgentId ?? actor.UserId;
			}
		}
		await store.SaveAsync(new ProjectAggregate(aggregate.Project, items), snapshot.Revision, actor.AgentId ?? actor.UserId, actor.IsHuman ? "Human" : "Agent", action, cancellationToken, actor.AgentNameSnapshot);
		aggregate.Project.Revision++;
		return ToSnapshot(new ProjectAggregate(aggregate.Project, items));
	}

	public async Task DeleteAsync(AuthenticatedActor actor, Guid projectId, long revision, bool confirmed, CancellationToken cancellationToken = default)
	{
		if (!confirmed)
			throw new PlanForgeException(400, "Confirm permanent project deletion.");
		ProjectAggregate aggregate = await store.GetAsync(actor.UserId, projectId, cancellationToken) ?? throw new PlanForgeException(404, "Project not found.");
		if (aggregate.Project.Revision != revision)
			throw new PlanForgeException(409, "The project changed. Reload before deleting.");
		await store.DeleteAsync(actor.UserId, projectId, revision, cancellationToken);
	}

	private static ProjectSnapshot ToSnapshot(ProjectAggregate aggregate)
		=> new(aggregate.Project.Id, aggregate.Project.Revision, aggregate.Project.Name, aggregate.Project.Description, aggregate.Project.Status.ToString(), aggregate.Project.Workflow.ToString(), new DateTimeOffset(DateTime.SpecifyKind(aggregate.Project.UpdatedAt, DateTimeKind.Utc)),
			aggregate.Items.Select(item => new PlanItem(item.Id, item.Kind.ToString(), item.ParentId, item.SortOrder, item.Title, item.Description, item.Kind == WorkItemKind.Decision && item.Status == WorkStatus.Completed ? "Decided" : item.Status.ToString(), item.AssignmentMode.ToString(), item.AssignedAgentId, item.Resolution, item.Evidence?.ToString(), item.CompletedBy, item.CompletedAt, item.CreationOrder)).ToList());
}
