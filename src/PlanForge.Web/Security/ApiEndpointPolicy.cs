namespace PlanForge.Web.Security;

/// <summary>Admits only the existing API origin and the documented local development endpoint.</summary>
public sealed class ApiEndpointPolicy
{
	public const string ProductionHost = "planforge-api-ftccgdhacyfmhmea.spaincentral-01.azurewebsites.net";
	private readonly Uri origin;

	public ApiEndpointPolicy(string? configuredAddress, bool development)
	{
		string? address = configuredAddress ?? (development ? "https://localhost:7601" : null);
		if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? candidate) || !string.IsNullOrEmpty(candidate.UserInfo) || !string.IsNullOrEmpty(candidate.Query) || !string.IsNullOrEmpty(candidate.Fragment) || candidate.AbsolutePath != "/")
			throw new InvalidOperationException("The configured PlanForge API origin is invalid.");
		bool production = candidate.Scheme == Uri.UriSchemeHttps && candidate.Host.Equals(ProductionHost, StringComparison.OrdinalIgnoreCase) && candidate.Port == 443;
		bool loopback = development && candidate.IsLoopback && candidate.Port == 7601 && candidate.Scheme is "http" or "https";
		if (!production && !loopback)
			throw new InvalidOperationException("The configured PlanForge API origin is not allowed.");
		origin = candidate;
		BaseUri = new Uri(origin, "api/v1/");
	}

	public Uri BaseUri { get; }

	public bool Allows(Uri? target)
		=> target is { IsAbsoluteUri: true } && target.Scheme == origin.Scheme && target.Host.Equals(origin.Host, StringComparison.OrdinalIgnoreCase) && target.Port == origin.Port
			&& string.IsNullOrEmpty(target.UserInfo) && string.IsNullOrEmpty(target.Fragment) && target.AbsolutePath.StartsWith("/api/v1/", StringComparison.Ordinal);
}
