namespace PlanForge.Application.Accounts;

using PlanForge.Application.Common;
using PlanForge.Contracts;
using PlanForge.Contracts.Accounts;
using PlanForge.Domain.Accounts;

public sealed class AccountManagementService(IAccountStore store, ISecretProtector secrets, TimeProvider timeProvider)
{
	public async Task ChangePasswordAsync(AuthenticatedActor actor, ChangePasswordRequest request, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		if (request.CurrentPassword is null || request.CurrentPassword.Length > 128 || request.NewPassword is null || request.NewPassword.Length is < 12 or > 128)
			throw new PlanForgeException(400, "Provide the current password and a new password of 12–128 characters.");
		UserAccount user = await GetUserAsync(actor, cancellationToken);
		if (!secrets.VerifyPassword(user.PasswordHash, request.CurrentPassword))
			throw new PlanForgeException(400, "The current password is incorrect.");
		user.PasswordHash = secrets.HashPassword(request.NewPassword);
		user.SecurityVersion = checked(user.SecurityVersion + 1);
		foreach (AccessCredential credential in await store.ListCredentialsAsync(actor.UserId, cancellationToken))
			credential.RevokedAt ??= timeProvider.GetUtcNow();
		await store.SaveAsync(cancellationToken);
	}

	public async Task<DownloadFile> GetAvatarAsync(AuthenticatedActor actor, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		UserAccount user = await GetUserAsync(actor, cancellationToken);
		if (user.AvatarBytes is null || user.AvatarContentType is null)
			throw new PlanForgeException(404, "Avatar not found.");
		AvatarValidator.Validate(user.AvatarBytes, user.AvatarContentType);
		return new DownloadFile(user.AvatarBytes, user.AvatarContentType, "avatar." + Extension(user.AvatarContentType), true);
	}

	public async Task<UserProfile> SetAvatarAsync(AuthenticatedActor actor, byte[]? content, string? contentType, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		if (content is not null)
			AvatarValidator.Validate(content, contentType ?? string.Empty);
		UserAccount user = await GetUserAsync(actor, cancellationToken);
		user.AvatarBytes = content;
		user.AvatarContentType = content is null ? null : contentType!.ToLowerInvariant();
		await store.SaveAsync(cancellationToken);
		return ToProfile(user);
	}

	public async Task<UserProfile> UpdateAsync(AuthenticatedActor actor, UpdateProfileRequest request, CancellationToken cancellationToken = default)
	{
		RequireHuman(actor);
		if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 80)
			throw new PlanForgeException(400, "Provide a display name of up to 80 characters.");
		UserAccount user = await GetUserAsync(actor, cancellationToken);
		user.DisplayName = request.DisplayName.Trim();
		await store.SaveAsync(cancellationToken);
		return ToProfile(user);
	}

	private static string Extension(string contentType) => contentType == "image/jpeg" ? "jpg" : contentType == "image/webp" ? "webp" : "png";
	private async Task<UserAccount> GetUserAsync(AuthenticatedActor actor, CancellationToken cancellationToken)
		=> await store.FindUserForUpdateAsync(actor.UserId, cancellationToken) ?? throw new PlanForgeException(401, "Account unavailable.");
	private static void RequireHuman(AuthenticatedActor actor)
	{
		if (!actor.IsHuman)
			throw new PlanForgeException(403, "This operation requires a human account session.");
	}
	private static UserProfile ToProfile(UserAccount user) => new(user.Id, user.Email, user.DisplayName, user.CreatedAt, user.AvatarBytes is not null);
}
