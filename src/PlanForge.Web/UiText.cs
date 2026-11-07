namespace PlanForge.Web;

using System.Globalization;
using System.Resources;

public sealed class UiText
{
	private static readonly ResourceManager resources = new("PlanForge.Web.Resources.UiText", typeof(UiText).Assembly);
	public static string Get(string key) => resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
