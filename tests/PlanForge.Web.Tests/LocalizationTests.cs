namespace PlanForge.Web.Tests;

using PlanForge.Web;
using System.Globalization;
using System.Resources;

public sealed class LocalizationTests
{
	[Fact]
	public void UiText_WhenUsedOutsideComponents_ReadsSameEnglishResourceWithoutChangingUserData()
	{
		Assert.Equal("{0}% of steps complete", UiText.Get("{0}% complete"));
		Assert.Equal("User supplied project title ñ", UiText.Get("User supplied project title ñ"));
	}

	[Theory]
	[InlineData("Revision {0}")]
	[InlineData("{0}% complete")]
	[InlineData("Save {0}")]
	[InlineData("{0} projects")]
	[InlineData("Expires {0}")]
	public void EnglishCatalog_WhenDynamicLabelUsed_HasEmbeddedFormat(string key)
	{
		var resources = new ResourceManager("PlanForge.Web.Resources.UiText", typeof(UiText).Assembly);
		Assert.NotNull(resources.GetString(key, CultureInfo.InvariantCulture));
	}

	[Theory]
	[InlineData("PlanForge.Web/Resources/UiText.resx")]
	[InlineData("PlanForge.Application/Resources/ErrorMessages.resx")]
	[InlineData("PlanForge.Application/Resources/ArchiveText.resx")]
	public void EnglishCatalog_WhenCompiled_HasNoCaseInsensitiveDuplicateKeys(string relativePath)
	{
		DirectoryInfo root = new(AppContext.BaseDirectory);
		while (!File.Exists(Path.Combine(root.FullName, "APP-WebApp-PlanForge.slnx"))) root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
		var catalog = System.Xml.Linq.XDocument.Load(Path.Combine(root.FullName, "src", relativePath));
		var duplicates = catalog.Root!.Elements("data").Select(element => element.Attribute("name")!.Value).GroupBy(key => key, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
		Assert.True(duplicates.Count == 0, "Case-insensitive resource collisions: " + string.Join(", ", duplicates));
	}

	[Theory]
	[InlineData("New {0}")]
	[InlineData("Edit {0}")]
	[InlineData("Local Changes + Manual Approval")]
	[InlineData("Branch + Pull Request")]
	[InlineData("Awaiting merge")]
	[InlineData("Added to your Library without a link. You can assign it later.")]
	[InlineData("Phase only: {0}")]
	[InlineData("The service returned an empty response.")]
	[InlineData("The service could not complete this request. Please try again.")]
	[InlineData("The service returned an invalid error response.")]
	[InlineData("Avatar updated")]
	[InlineData("New agent")]
	[InlineData("Edit agent")]
	public void EnglishCatalog_WhenDynamicLabelUsed_ContainsItsKey(string key)
		=> Assert.NotNull(new ResourceManager("PlanForge.Web.Resources.UiText", typeof(UiText).Assembly).GetString(key, CultureInfo.InvariantCulture));

	[Fact]
	public void EnglishCatalog_WhenUiRequestsLiteralKeys_ContainsEveryApplicationLabel()
	{
		DirectoryInfo root = new(AppContext.BaseDirectory);
		while (!File.Exists(Path.Combine(root.FullName, "APP-WebApp-PlanForge.slnx"))) root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
		var resources = new ResourceManager("PlanForge.Web.Resources.UiText", typeof(UiText).Assembly);
		var missing = new SortedSet<string>(StringComparer.Ordinal);
		foreach (string path in Directory.EnumerateFiles(Path.Combine(root.FullName, "src/PlanForge.Web"), "*", SearchOption.AllDirectories).Where(path => (path.EndsWith(".razor", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal)) && !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal)))
			foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), "(?:T\\[|UiText\\.Get\\(|ApiException\\([^,\\r\\n]+,\\s*)\\\"([^\\\"]+)\\\""))
				if (resources.GetString(match.Groups[1].Value, CultureInfo.InvariantCulture) is null) missing.Add(match.Groups[1].Value);
		Assert.True(missing.Count == 0, "Missing English resources: " + string.Join(" | ", missing));
	}

	[Fact]
	public void EnglishCatalog_WhenLoaded_HasRealEmbeddedUiResources()
	{
		var resources = new ResourceManager("PlanForge.Web.Resources.UiText", typeof(UiText).Assembly);
		Assert.Equal("Welcome back", resources.GetString("Welcome back", CultureInfo.InvariantCulture));
		Assert.Equal("Clean orphan resources", resources.GetString("Clean orphan resources", CultureInfo.InvariantCulture));
		Assert.Equal("Project exported successfully", resources.GetString("Project exported successfully", CultureInfo.InvariantCulture));
	}
}
