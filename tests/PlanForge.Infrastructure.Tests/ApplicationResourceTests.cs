namespace PlanForge.Infrastructure.Tests;

using PlanForge.Application.Common;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;

public sealed class ApplicationResourceTests
{
	[Fact]
	public void EnglishErrors_WhenUsedByApplication_ExistInEmbeddedResourceCatalog()
	{
		DirectoryInfo root = new(AppContext.BaseDirectory);
		while (!File.Exists(Path.Combine(root.FullName, "APP-WebApp-PlanForge.slnx"))) root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
		var resources = new ResourceManager("PlanForge.Application.Resources.ErrorMessages", typeof(PlanForgeException).Assembly);
		var missing = new SortedSet<string>(StringComparer.Ordinal);
		foreach (string layer in new[] { "Application", "Infrastructure", "Api" })
			foreach (string path in Directory.EnumerateFiles(Path.Combine(root.FullName, "src/PlanForge." + layer), "*.cs", SearchOption.AllDirectories).Where(path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/Migrations/", StringComparison.Ordinal)))
				foreach (Match match in Regex.Matches(File.ReadAllText(path), "(?:PlanForgeException\\(\\s*[0-9]+,\\s*|Invalid\\()\"([^\"]+)\""))
					if (resources.GetString(match.Groups[1].Value, CultureInfo.InvariantCulture) is null) missing.Add(match.Groups[1].Value);
		Assert.True(missing.Count == 0, "Missing application error resources: " + string.Join(" | ", missing));
	}

	[Fact]
	public void ApplicationError_WhenResourceKeySupplied_UsesEnglishMessage()
		=> Assert.Equal("Request failed", new PlanForgeException(400, "RequestFailedTitle").Message);
}
