namespace PlanForge.Web.Security;

using Microsoft.AspNetCore.Localization;

/// <summary>Uses an account preference, never a URL or browser language, for application text.</summary>
public sealed class AccountCultureProvider : RequestCultureProvider
{
	public const string LanguageClaim = "planforge:language";
	public static IReadOnlyList<string> SupportedLanguages { get; } = Array.AsReadOnly(new[] { "en" });

	public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
	{
		string? preference = httpContext.User.FindFirst(LanguageClaim)?.Value;
		string language = preference is not null && SupportedLanguages.Contains(preference, StringComparer.Ordinal) ? preference : "en";
		return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(language));
	}
}
