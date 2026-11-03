namespace PlanForge.Infrastructure.Planning;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Domain.Planning;
using PlanForge.Infrastructure.Database;

internal static class ProjectEventWriter
{
	/// <summary>Touches an owned project inside the caller's transaction without overwriting its plan.</summary>
	public static async Task TouchAsync(PlanForgeDbContext database, AuthenticatedActor actor, Guid projectId, CancellationToken cancellationToken)
	{
		int changed = await database.Projects.Where(project => project.Id == projectId && project.OwnerUserId == actor.UserId)
			.ExecuteUpdateAsync(setters => setters.SetProperty(project => project.Revision, project => project.Revision + 1).SetProperty(project => project.UpdatedAt, DateTime.UtcNow), cancellationToken);
		if (changed != 1)
			throw new PlanForgeException(404, "Project not found.");
		long revision = await database.Projects.Where(project => project.Id == projectId && project.OwnerUserId == actor.UserId).Select(project => project.Revision).SingleAsync(cancellationToken);
		var change = new ProjectChange { ProjectId = projectId, Revision = revision, ActorId = actor.AgentId ?? actor.UserId, ActorKind = actor.IsHuman ? "Human" : "Agent", Action = "Project updated", CreatedAt = DateTime.UtcNow };
		database.ProjectChanges.Add(change);
		await database.SaveChangesAsync(cancellationToken);
		List<long> expired = await database.ProjectChanges.Where(entry => entry.ProjectId == projectId).OrderByDescending(entry => entry.Id).Skip(100).Select(entry => entry.Id).ToListAsync(cancellationToken);
		await database.ProjectChanges.Where(entry => expired.Contains(entry.Id)).ExecuteDeleteAsync(cancellationToken);
		database.Entry(change).State = EntityState.Detached;
	}
}
