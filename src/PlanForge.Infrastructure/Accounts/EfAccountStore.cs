namespace PlanForge.Infrastructure.Accounts;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Database;

public sealed class EfAccountStore(PlanForgeDbContext database) : IAccountStore
{
	public Task AddCredentialAsync(AccessCredential credential, CancellationToken cancellationToken)
	{
		database.AccessCredentials.Add(credential);
		return Task.CompletedTask;
	}

	public Task AddUserAsync(UserAccount user, CancellationToken cancellationToken)
	{
		database.Users.Add(user);
		return Task.CompletedTask;
	}

	public Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
		=> MetadataQuery().SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

	public Task<AccessCredential?> FindCredentialAsync(string tokenHash, CancellationToken cancellationToken)
		=> database.AccessCredentials.SingleOrDefaultAsync(credential => credential.TokenHash == tokenHash, cancellationToken);

	public Task<AccessCredential?> FindCredentialByIdAsync(Guid credentialId, Guid ownerUserId, CancellationToken cancellationToken)
		=> database.AccessCredentials.SingleOrDefaultAsync(credential => credential.Id == credentialId && credential.OwnerUserId == ownerUserId, cancellationToken);

	public Task<UserAccount?> FindUserAsync(Guid userId, CancellationToken cancellationToken)
		=> MetadataQuery().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

	public Task<string?> GetActiveAgentNameAsync(Guid ownerId, Guid agentId, long credentialVersion, CancellationToken cancellationToken)
		=> database.Agents.Where(agent => agent.OwnerUserId == ownerId && agent.Id == agentId && agent.IsActive && agent.CredentialVersion == credentialVersion).Select(agent => (string?)agent.Name).SingleOrDefaultAsync(cancellationToken);

	public Task<UserAccount?> FindUserForUpdateAsync(Guid userId, CancellationToken cancellationToken)
		=> database.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

	public async Task<IReadOnlyList<AccessCredential>> ListCredentialsAsync(Guid ownerId, CancellationToken cancellationToken)
		=> await database.AccessCredentials.Where(credential => credential.OwnerUserId == ownerId).ToListAsync(cancellationToken);

	public async Task SaveAsync(CancellationToken cancellationToken)
	{
		try
		{
			var rotations = database.ChangeTracker.Entries<UserAccount>().Where(entry => entry.State == EntityState.Modified && entry.Property(user => user.SecurityVersion).IsModified).OrderBy(entry => entry.Entity.Id).ToList();
			if (rotations.Count == 0)
			{
				await database.SaveChangesAsync(cancellationToken);
				return;
			}
			await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
			foreach (var entry in rotations)
			{
				long originalVersion = entry.Property(user => user.SecurityVersion).OriginalValue;
				Guid ownerId = entry.Entity.Id;
				int locked = await database.Users.Where(user => user.Id == ownerId && user.SecurityVersion == originalVersion)
					.ExecuteUpdateAsync(setters => setters.SetProperty(user => user.SecurityVersion, user => user.SecurityVersion), cancellationToken);
				if (locked != 1)
					throw new DbUpdateConcurrencyException("The account changed concurrently.");
				DateTimeOffset revokedAt = DateTimeOffset.UtcNow;
				await database.AccessCredentials.Where(credential => credential.OwnerUserId == ownerId && credential.RevokedAt == null)
					.ExecuteUpdateAsync(setters => setters.SetProperty(credential => credential.RevokedAt, (DateTimeOffset?)revokedAt), cancellationToken);
			}
			await database.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
		}
		catch (DbUpdateConcurrencyException)
		{
			throw new PlanForgeException(409, "The account changed concurrently. Reload before retrying.");
		}
	}

	private IQueryable<UserAccount> MetadataQuery()
		=> database.Users.AsNoTracking().Select(user => new UserAccount
		{
			Id = user.Id,
			Email = user.Email,
			NormalizedEmail = user.NormalizedEmail,
			DisplayName = user.DisplayName,
			Language = user.Language,
			CreatedAt = user.CreatedAt,
			IsActive = user.IsActive,
			PasswordHash = user.PasswordHash,
			SecurityVersion = user.SecurityVersion,
			AvatarBytes = user.AvatarBytes == null ? null : Array.Empty<byte>(),
			AvatarContentType = user.AvatarContentType
		});
}
