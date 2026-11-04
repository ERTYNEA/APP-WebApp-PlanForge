namespace PlanForge.Web.Tests;

using PlanForge.Web;
using System.Globalization;
using System.Resources;

public sealed class LocalizationTests
{
	[Fact]
	public void EnglishCatalog_WhenLoaded_HasRealEmbeddedUiResources()
	{
		var resources=new ResourceManager("PlanForge.Web.Resources.UiText",typeof(UiText).Assembly);
		Assert.Equal("Welcome back",resources.GetString("Welcome back",CultureInfo.InvariantCulture));
		Assert.Equal("Clean orphan resources",resources.GetString("Clean orphan resources",CultureInfo.InvariantCulture));
		Assert.Equal("Project exported successfully",resources.GetString("Project exported successfully",CultureInfo.InvariantCulture));
	}
}
