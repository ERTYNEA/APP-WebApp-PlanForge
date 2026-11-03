namespace PlanForge.Contracts;

public static class ProductVersion
{
	public static string Informational => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(ProductVersion).Assembly)?.InformationalVersion ?? "unknown";
}
