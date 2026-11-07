namespace PlanForge.Application.Accounts;

using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;
using PlanForge.Domain.Accounts;

public sealed class AccountService(IAccountStore accountStore, ISecretProtector secretProtector, TimeProvider timeProvider)
{
	public async Task<AuthenticatedActor> AuthenticateAsync(string token, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(token) || token.Length != 69 || !(token.StartsWith("pf_h_", StringComparison.Ordinal) || token.StartsWith("pf_a_", StringComparison.Ordinal)))
			throw new PlanForgeException(401, "A valid access token is required.");
		AccessCredential? credential = await accountStore.FindCredentialAsync(secretProtector.HashToken(token), cancellationToken);
		if (credential is null || credential.RevokedAt is not null)
			throw new PlanForgeException(401, "The access token is invalid or revoked.");
		if (credential.ExpiresAt <= timeProvider.GetUtcNow())
			throw new PlanForgeException(401, "The access token has expired. Sign in again or renew your agent token.");
		UserAccount? user = await accountStore.FindUserAsync(credential.OwnerUserId, cancellationToken);
		if (user is null || !user.IsActive || credential.SecurityVersion != user.SecurityVersion || (credential.Kind == CredentialKind.HumanSession) != token.StartsWith("pf_h_", StringComparison.Ordinal))
			throw new PlanForgeException(401, "The access token is invalid or revoked.");
		string? agentName = null;
		if (credential.Kind == CredentialKind.AgentToken && credential.AgentId is Guid agentId)
		{
			agentName = await accountStore.GetActiveAgentNameAsync(user.Id, agentId, credential.AgentCredentialVersion, cancellationToken);
			if (agentName is null)
				throw new PlanForgeException(401, "The agent is inactive or unavailable. Renew an authorized agent token.");
		}
		return new AuthenticatedActor(user.Id, credential.Kind, credential.Id, credential.AgentId, credential.SecurityVersion, agentName);
	}

	public async Task<UserProfile> GetProfileAsync(AuthenticatedActor actor, CancellationToken cancellationToken = default)
	{
		UserAccount? user = await accountStore.FindUserAsync(actor.UserId, cancellationToken);
		if (user is null || !user.IsActive)
			throw new PlanForgeException(401, "The account is unavailable.");
		return new UserProfile(user.Id, user.Email, user.DisplayName, user.CreatedAt, user.AvatarBytes is not null, user.Language);
	}

	public async Task<AccessGrant> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || request.Password is null || request.Password.Length > 128)
			throw new PlanForgeException(401, "Invalid email or password.");
		UserAccount? user = await accountStore.FindByEmailAsync(request.Email.Trim().ToUpperInvariant(), cancellationToken);
		if (user is null || !user.IsActive || !secretProtector.VerifyPassword(user.PasswordHash, request.Password))
			throw new PlanForgeException(401, "Invalid email or password.");
		return await IssueSessionAsync(user, cancellationToken);
	}

	public async Task LogoutAsync(AuthenticatedActor actor, CancellationToken cancellationToken = default)
	{
		AccessCredential? credential = await accountStore.FindCredentialByIdAsync(actor.CredentialId, actor.UserId, cancellationToken);
		if (credential is not null)
		{
			credential.RevokedAt = timeProvider.GetUtcNow();
			await accountStore.SaveAsync(cancellationToken);
		}
	}

	public async Task<AccessGrant> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || !System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _) || request.Password is null || request.Password.Length < 12 || request.Password.Length > 128 || string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 80)
			throw new PlanForgeException(400, "Enter a valid email, a 12–128 character password and a display name of up to 80 characters.");
		if (await accountStore.FindByEmailAsync(request.Email.Trim().ToUpperInvariant(), cancellationToken) is not null)
			throw new PlanForgeException(409, "An account with this email already exists.");
		var user = new UserAccount
		{
			CreatedAt = timeProvider.GetUtcNow(),
			DisplayName = request.DisplayName.Trim(),
			Email = request.Email.Trim(),
			NormalizedEmail = request.Email.Trim().ToUpperInvariant(),
			PasswordHash = secretProtector.HashPassword(request.Password)
		};
		await accountStore.AddUserAsync(user, cancellationToken);
		return await IssueSessionAsync(user, cancellationToken);
	}

	private async Task<AccessGrant> IssueSessionAsync(UserAccount user, CancellationToken cancellationToken)
	{
		string token = secretProtector.CreateToken(false);
		DateTimeOffset now = timeProvider.GetUtcNow();
		var credential = new AccessCredential
		{
			CreatedAt = now,
			ExpiresAt = now.AddDays(7),
			Kind = CredentialKind.HumanSession,
			Name = "Web session",
			OwnerUserId = user.Id,
			SecurityVersion = user.SecurityVersion,
			TokenHash = secretProtector.HashToken(token)
		};
		await accountStore.AddCredentialAsync(credential, cancellationToken);
		await accountStore.SaveAsync(cancellationToken);
		return new AccessGrant(token, credential.ExpiresAt, new UserProfile(user.Id, user.Email, user.DisplayName, user.CreatedAt, user.AvatarBytes is not null, user.Language));
	}
}
