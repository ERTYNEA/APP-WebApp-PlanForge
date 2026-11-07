namespace PlanForge.Infrastructure.Agents;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Agents;
using PlanForge.Application.Common;
using PlanForge.Domain.Accounts;
using PlanForge.Domain.Agents;
using PlanForge.Infrastructure.Database;

public sealed class EfAgentStore(PlanForgeDbContext database) : IAgentStore
{
	public async Task<int> CleanTokensAsync(Guid ownerId, DateTimeOffset now, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
		// Keep eligibility in the DELETE itself: no owner-wide metadata buffer or stale ID set.
		IQueryable<AccessCredential> eligible = database.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
			? database.AccessCredentials.FromSqlInterpolated($"SELECT * FROM AccessCredentials WHERE OwnerUserId = {ownerId} AND Kind = {(int)CredentialKind.AgentToken} AND (RevokedAt IS NOT NULL OR julianday(ExpiresAt) <= julianday({now}))")
			: database.AccessCredentials.Where(token => token.OwnerUserId == ownerId && token.Kind == CredentialKind.AgentToken && (token.RevokedAt != null || token.ExpiresAt <= now));
		int deleted = await eligible.ExecuteDeleteAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return deleted;
	}

	public async Task DeleteAsync(Guid ownerId, Guid agentId, CancellationToken cancellationToken)
	{
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		int locked = await database.Agents.Where(agent => agent.OwnerUserId == ownerId && agent.Id == agentId)
			.ExecuteUpdateAsync(setters => setters.SetProperty(agent => agent.IsActive, false), cancellationToken);
		if (locked != 1)
			throw new PlanForgeException(404, "Agent not found.");
		string name = await database.Agents.Where(agent => agent.Id == agentId && agent.OwnerUserId == ownerId).Select(agent => agent.Name).SingleAsync(cancellationToken);
		await database.ProjectChanges.Where(change => change.ActorId == agentId && change.ActorKind == "Agent" && change.ActorNameSnapshot == null && database.Projects.Any(project => project.Id == change.ProjectId && project.OwnerUserId == ownerId))
			.ExecuteUpdateAsync(setters => setters.SetProperty(change => change.ActorNameSnapshot, name), cancellationToken);
		List<Guid> projects = await database.WorkItems.Where(item => item.AssignedAgentId == agentId && database.Projects.Any(project => project.Id == item.ProjectId && project.OwnerUserId == ownerId))
			.Select(item => item.ProjectId).Distinct().ToListAsync(cancellationToken);
		foreach (Guid projectId in projects.Order())
		{
			await Planning.ProjectEventWriter.TouchAsync(database, new Application.Accounts.AuthenticatedActor(ownerId, CredentialKind.HumanSession, Guid.Empty, null), projectId, cancellationToken);
			await database.WorkItems.Where(item => item.ProjectId == projectId && item.AssignedAgentId == agentId)
				.ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AssignedAgentId, (Guid?)null)
					.SetProperty(item => item.AssignmentMode, Domain.Planning.AssignmentMode.Inherit).SetProperty(item => item.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
		}
		await database.AccessCredentials.Where(token => token.OwnerUserId == ownerId && token.AgentId == agentId).ExecuteDeleteAsync(cancellationToken);
		await database.Agents.Where(agent => agent.OwnerUserId == ownerId && agent.Id == agentId).ExecuteDeleteAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
	}

	public void Add(Agent agent) => database.Agents.Add(agent);
	public void Add(AccessCredential credential) => database.AccessCredentials.Add(credential);
	public Task<Agent?> GetAsync(Guid ownerId, Guid agentId, CancellationToken cancellationToken)
		=> database.Agents.SingleOrDefaultAsync(agent => agent.OwnerUserId == ownerId && agent.Id == agentId, cancellationToken);
	public Task<AccessCredential?> GetTokenAsync(Guid ownerId, Guid tokenId, CancellationToken cancellationToken)
		=> database.AccessCredentials.SingleOrDefaultAsync(token => token.OwnerUserId == ownerId && token.Id == tokenId && token.Kind == CredentialKind.AgentToken, cancellationToken);
	public async Task<IReadOnlyList<Agent>> ListAsync(Guid ownerId, CancellationToken cancellationToken)
		=> await database.Agents.AsNoTracking().Where(agent => agent.OwnerUserId == ownerId).OrderBy(agent => agent.Name)
			.Select(agent => new Agent { Id = agent.Id, OwnerUserId = agent.OwnerUserId, Name = agent.Name, Description = agent.Description, IsActive = agent.IsActive, CreatedAt = agent.CreatedAt, AvatarBytes = agent.AvatarBytes == null ? null : Array.Empty<byte>() }).ToListAsync(cancellationToken);
	public async Task<IReadOnlyList<AccessCredential>> ListTokensAsync(Guid ownerId, CancellationToken cancellationToken)
		=> await database.AccessCredentials.AsNoTracking().Where(token => token.OwnerUserId == ownerId && token.Kind == CredentialKind.AgentToken)
			.Select(token => new AccessCredential { Id = token.Id, OwnerUserId = token.OwnerUserId, Kind = token.Kind, Name = token.Name, AgentId = token.AgentId, CreatedAt = token.CreatedAt, ExpiresAt = token.ExpiresAt, RevokedAt = token.RevokedAt, LastUsedAt = token.LastUsedAt }).ToListAsync(cancellationToken);
	/// <summary>Commits agent changes and token issuance with one generation/revocation lock protocol.</summary>
	public async Task SaveAsync(CancellationToken cancellationToken)
	{
		var ownedChanges = database.ChangeTracker.Entries().Where(entry => (entry.Entity is Agent or AccessCredential) && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
		try
		{
			foreach (AccessCredential credential in ownedChanges.Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).OfType<AccessCredential>().Where(credential => credential.Kind == CredentialKind.AgentToken).OrderBy(credential => credential.OwnerUserId))
			{
				int admitted = await database.Users.Where(user => user.Id == credential.OwnerUserId && user.IsActive && user.SecurityVersion == credential.SecurityVersion)
					.ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityVersion, user => user.SecurityVersion), cancellationToken);
				if (admitted != 1)
					throw new PlanForgeException(409, "The account changed while issuing this token. Sign in again before retrying.");
			}
			foreach (var entry in ownedChanges.Where(entry => entry.State == EntityState.Modified && entry.Entity is Agent).OrderBy(entry => ((Agent)entry.Entity).Id))
			{
				Agent agent = (Agent)entry.Entity;
				long originalVersion = (long)entry.OriginalValues[nameof(Agent.CredentialVersion)]!;
				int locked = await database.Agents.Where(current => current.OwnerUserId == agent.OwnerUserId && current.Id == agent.Id && current.CredentialVersion == originalVersion)
					.ExecuteUpdateAsync(setters => setters.SetProperty(current => current.CredentialVersion, current => current.CredentialVersion), cancellationToken);
				if (locked != 1)
					throw new DbUpdateConcurrencyException("The agent changed concurrently.");
				if (!agent.IsActive && entry.Property(nameof(Agent.CredentialVersion)).IsModified)
				{
					DateTimeOffset revokedAt = DateTimeOffset.UtcNow;
					await database.AccessCredentials.Where(credential => credential.OwnerUserId == agent.OwnerUserId && credential.AgentId == agent.Id && credential.Kind == CredentialKind.AgentToken && credential.RevokedAt == null)
						.ExecuteUpdateAsync(setters => setters.SetProperty(credential => credential.RevokedAt, (DateTimeOffset?)revokedAt), cancellationToken);
				}
			}
			foreach (AccessCredential credential in ownedChanges.Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).OfType<AccessCredential>().Where(credential => credential.Kind == CredentialKind.AgentToken && credential.AgentId.HasValue).OrderBy(credential => credential.AgentId))
			{
				int admitted = await database.Agents.Where(agent => agent.OwnerUserId == credential.OwnerUserId && agent.Id == credential.AgentId && agent.IsActive && agent.CredentialVersion == credential.AgentCredentialVersion)
					.ExecuteUpdateAsync(setters => setters.SetProperty(agent => agent.CredentialVersion, agent => agent.CredentialVersion), cancellationToken);
				if (admitted != 1)
					throw new PlanForgeException(409, "The agent changed while issuing this token. Reload and issue a new token.");
			}
			await database.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
		}
		catch
		{
			foreach (var entry in ownedChanges)
				entry.State = EntityState.Detached;
			throw;
		}
	}
}
