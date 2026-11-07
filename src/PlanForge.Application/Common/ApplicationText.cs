namespace PlanForge.Application.Common;

using System.Globalization;
using System.Resources;

/// <summary>Application-owned messages, independent of user-authored business content.</summary>
public static class ApplicationText
{
	private static readonly ResourceManager resources = new("PlanForge.Application.Resources.ErrorMessages", typeof(ApplicationText).Assembly);
	public static string Get(string key) => resources.GetString(key, CultureInfo.GetCultureInfo("en")) ?? key;
}
