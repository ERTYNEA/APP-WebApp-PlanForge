namespace PlanForge.Infrastructure.Accounts;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
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
		=> database.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

	public Task<AccessCredential?> FindCredentialAsync(string tokenHash, CancellationToken cancellationToken)
		=> database.AccessCredentials.SingleOrDefaultAsync(credential => credential.TokenHash == tokenHash, cancellationToken);

	public Task<AccessCredential?> FindCredentialByIdAsync(Guid credentialId, Guid ownerUserId, CancellationToken cancellationToken)
		=> database.AccessCredentials.SingleOrDefaultAsync(credential => credential.Id == credentialId && credential.OwnerUserId == ownerUserId, cancellationToken);

	public Task<UserAccount?> FindUserAsync(Guid userId, CancellationToken cancellationToken)
		=> database.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

	public async Task SaveAsync(CancellationToken cancellationToken)
		=> await database.SaveChangesAsync(cancellationToken);
}
