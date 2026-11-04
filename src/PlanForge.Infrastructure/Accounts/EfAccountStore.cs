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

	public Task<bool> IsAgentActiveAsync(Guid ownerId, Guid agentId, CancellationToken cancellationToken)
		=> database.Agents.AnyAsync(agent => agent.OwnerUserId == ownerId && agent.Id == agentId && agent.IsActive, cancellationToken);

	public Task<UserAccount?> FindUserForUpdateAsync(Guid userId, CancellationToken cancellationToken)
		=> database.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

	public async Task<IReadOnlyList<AccessCredential>> ListCredentialsAsync(Guid ownerId, CancellationToken cancellationToken)
		=> await database.AccessCredentials.Where(credential => credential.OwnerUserId == ownerId).ToListAsync(cancellationToken);

	public async Task SaveAsync(CancellationToken cancellationToken)
	{
		try
		{
			await database.SaveChangesAsync(cancellationToken);
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
			CreatedAt = user.CreatedAt,
			IsActive = user.IsActive,
			PasswordHash = user.PasswordHash,
			SecurityVersion = user.SecurityVersion,
			AvatarBytes = user.AvatarBytes == null ? null : Array.Empty<byte>(),
			AvatarContentType = user.AvatarContentType
		});
}
