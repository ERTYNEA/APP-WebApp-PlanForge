namespace PlanForge.Application.Agents;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Contracts;
using PlanForge.Contracts.Agents;
using PlanForge.Domain.Accounts;
using PlanForge.Domain.Agents;

public sealed class AgentService(IAgentStore store, ISecretProtector secrets, TimeProvider timeProvider)
{
	public async Task<AgentProfile> CreateAsync(AuthenticatedActor actor, AgentRequest request, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		ValidateAgent(request);
		var agent = new Agent { Name = request.Name.Trim(), Description = request.Description, OwnerUserId = actor.UserId, CreatedAt = timeProvider.GetUtcNow(), IsActive = request.IsActive };
		store.Add(agent);
		await store.SaveAsync(cancellationToken);
		return ToProfile(agent);
	}

	public async Task<AgentTokenGrant> CreateTokenAsync(AuthenticatedActor actor, CreateAgentTokenRequest request, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		AgentTokenGrant grant = await PrepareTokenAsync(actor, request, cancellationToken);
		await store.SaveAsync(cancellationToken);
		return grant;
	}

	public async Task<AgentTokenGrant> RenewTokenAsync(AuthenticatedActor actor, Guid tokenId, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		AccessCredential original = await store.GetTokenAsync(actor.UserId, tokenId, cancellationToken) ?? throw new PlanForgeException(404, "Agent token not found.");
		AgentTokenGrant grant = await PrepareTokenAsync(actor, new CreateAgentTokenRequest(original.Name, original.AgentId), cancellationToken);
		original.RevokedAt = timeProvider.GetUtcNow();
		await store.SaveAsync(cancellationToken);
		return grant;
	}
	public async Task RevokeTokenAsync(AuthenticatedActor actor, Guid tokenId, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		AccessCredential credential = await store.GetTokenAsync(actor.UserId, tokenId, cancellationToken) ?? throw new PlanForgeException(404, "Agent token not found.");
		credential.RevokedAt = timeProvider.GetUtcNow();
		await store.SaveAsync(cancellationToken);
	}
	public async Task<IReadOnlyList<AgentTokenProfile>> ListTokensAsync(AuthenticatedActor actor, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		return (await store.ListTokensAsync(actor.UserId, cancellationToken)).OrderByDescending(token => token.CreatedAt).Select(ToTokenProfile).ToList();
	}

	public async Task<AgentProfile> UpdateAsync(AuthenticatedActor actor, Guid agentId, AgentRequest request, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		ValidateAgent(request);
		Agent agent = await store.GetAsync(actor.UserId, agentId, cancellationToken) ?? throw new PlanForgeException(404, "Agent not found.");
		agent.Name = request.Name.Trim();
		agent.Description = request.Description;
		agent.IsActive = request.IsActive;
		if (!agent.IsActive)
			foreach (AccessCredential token in (await store.ListTokensAsync(actor.UserId, cancellationToken)).Where(token => token.AgentId == agent.Id && token.RevokedAt is null))
				token.RevokedAt = timeProvider.GetUtcNow();
		await store.SaveAsync(cancellationToken);
		return ToProfile(agent);
	}
	public async Task<IReadOnlyList<AgentProfile>> ListAsync(AuthenticatedActor actor, CancellationToken cancellationToken = default)
		=> (await store.ListAsync(actor.UserId, cancellationToken)).Select(ToProfile).ToList();

	public async Task<AgentProfile> SetAvatarAsync(AuthenticatedActor actor, Guid agentId, byte[]? content, string? contentType, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		if (content is not null)
			AvatarValidator.Validate(content, contentType ?? string.Empty);
		Agent agent = await store.GetAsync(actor.UserId, agentId, cancellationToken) ?? throw new PlanForgeException(404, "Agent not found.");
		agent.AvatarBytes = content;
		agent.AvatarContentType = content is null ? null : contentType!.ToLowerInvariant();
		await store.SaveAsync(cancellationToken);
		return ToProfile(agent);
	}
	public async Task<DownloadFile> GetAvatarAsync(AuthenticatedActor actor, Guid agentId, CancellationToken cancellationToken = default)
	{
		Agent agent = await store.GetAsync(actor.UserId, agentId, cancellationToken) ?? throw new PlanForgeException(404, "Agent not found.");
		if (agent.AvatarBytes is null || agent.AvatarContentType is null)
			throw new PlanForgeException(404, "Avatar not found.");
		AvatarValidator.Validate(agent.AvatarBytes, agent.AvatarContentType);
		string extension = agent.AvatarContentType == "image/jpeg" ? "jpg" : agent.AvatarContentType == "image/webp" ? "webp" : "png";
		return new DownloadFile(agent.AvatarBytes, agent.AvatarContentType, "agent-avatar." + extension, true);
	}

	private async Task<AgentTokenGrant> PrepareTokenAsync(AuthenticatedActor actor, CreateAgentTokenRequest request, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80)
			throw new PlanForgeException(400, "Provide a token name of up to 80 characters.");
		if (request.AgentId is Guid agentId)
		{
			Agent? agent = await store.GetAsync(actor.UserId, agentId, cancellationToken);
			if (agent is null || !agent.IsActive)
				throw new PlanForgeException(404, "Active agent not found.");
		}
		DateTimeOffset now = timeProvider.GetUtcNow();
		string token = secrets.CreateToken(true);
		var credential = new AccessCredential { Name = request.Name.Trim(), Kind = CredentialKind.AgentToken, OwnerUserId = actor.UserId, SecurityVersion = actor.SecurityVersion, AgentId = request.AgentId, CreatedAt = now, ExpiresAt = now.AddDays(30), TokenHash = secrets.HashToken(token) };
		store.Add(credential);
		return new AgentTokenGrant(token, ToTokenProfile(credential));
	}

	private static void RequireHuman(AuthenticatedActor actor)
	{
		if (!actor.IsHuman)
			throw new PlanForgeException(403, "Only a human account session may manage agents and tokens.");
	}

	private static AgentProfile ToProfile(Agent agent) => new(agent.Id, agent.Name, agent.Description, agent.IsActive, agent.AvatarBytes is not null);
	private static AgentTokenProfile ToTokenProfile(AccessCredential credential) => new(credential.Id, credential.Name, credential.AgentId, credential.CreatedAt, credential.ExpiresAt, credential.RevokedAt, credential.LastUsedAt);
	private static void ValidateAgent(AgentRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80 || request.Description is null || request.Description.Length > 2000)
			throw new PlanForgeException(400, "Provide an agent name of up to 80 characters and a description of up to 2000 characters.");
	}
}
