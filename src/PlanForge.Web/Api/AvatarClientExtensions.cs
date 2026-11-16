namespace PlanForge.Web.Api;

using PlanForge.Contracts.Accounts;

internal static class AvatarClientExtensions
{
	/// <summary>Reads current avatar metadata from the owning human or agent profile endpoint.</summary>
	public static Task<AvatarState> GetAvatarStateAsync(this PlanForgeClient api, string avatarPath, CancellationToken cancellationToken = default)
		=> api.SendAvatarAsync(HttpMethod.Get, avatarPath[..^"/avatar".Length], cancellationToken: cancellationToken);

	/// <summary>Projects the avatar metadata from the existing human or agent profile mutation receipt.</summary>
	public static async Task<AvatarState> SendAvatarAsync(this PlanForgeClient api, HttpMethod method, string path, byte[]? bytes = null, string? contentType = null, CancellationToken cancellationToken = default)
	{
		AvatarProfileReceipt receipt = await api.SendAsync<AvatarProfileReceipt>(method, path, bytes: bytes, contentType: contentType, cancellationToken: cancellationToken);
		return receipt is null ? throw new ApiException(502, UiText.Get("The service returned an empty response.")) : new(receipt.HasAvatar, receipt.AvatarVersion);
	}

	private sealed record AvatarProfileReceipt(bool HasAvatar, Guid AvatarVersion);
}
