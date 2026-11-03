namespace PlanForge.Infrastructure.Agents;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Agents;
using PlanForge.Domain.Accounts;
using PlanForge.Domain.Agents;
using PlanForge.Infrastructure.Database;

public sealed class EfAgentStore(PlanForgeDbContext database) : IAgentStore
{
	public void Add(Agent agent) => database.Agents.Add(agent);
	public void Add(AccessCredential credential) => database.AccessCredentials.Add(credential);
	public Task<Agent?> GetAsync(Guid ownerId, Guid agentId, CancellationToken cancellationToken)
		=> database.Agents.SingleOrDefaultAsync(agent => agent.OwnerUserId == ownerId && agent.Id == agentId, cancellationToken);
	public Task<AccessCredential?> GetTokenAsync(Guid ownerId, Guid tokenId, CancellationToken cancellationToken)
		=> database.AccessCredentials.SingleOrDefaultAsync(token => token.OwnerUserId == ownerId && token.Id == tokenId && token.Kind == CredentialKind.AgentToken, cancellationToken);
	public async Task<IReadOnlyList<Agent>> ListAsync(Guid ownerId, CancellationToken cancellationToken)
		=> await database.Agents.Where(agent => agent.OwnerUserId == ownerId).OrderBy(agent => agent.Name).ToListAsync(cancellationToken);
	public async Task<IReadOnlyList<AccessCredential>> ListTokensAsync(Guid ownerId, CancellationToken cancellationToken)
		=> await database.AccessCredentials.Where(token => token.OwnerUserId == ownerId && token.Kind == CredentialKind.AgentToken).ToListAsync(cancellationToken);
	public async Task SaveAsync(CancellationToken cancellationToken) => await database.SaveChangesAsync(cancellationToken);
}
