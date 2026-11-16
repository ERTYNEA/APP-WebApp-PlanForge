namespace PlanForge.Web.Tests;

using PlanForge.Contracts.Accounts;

internal static class AvatarTestResponses
{
	public static HttpResponseMessage Json(AvatarState state)
		=> ComponentTestContext.Json(new { state.HasAvatar, AvatarVersion = state.Version });
}
