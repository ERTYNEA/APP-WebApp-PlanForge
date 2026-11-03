namespace PlanForge.Api.Library;

using Microsoft.Extensions.Configuration;
using PlanForge.Application.Library;
using System.Globalization;

public static class ResourceLimitConfiguration
{
	public static ResourceLimits Read(IConfiguration configuration)
	{
		var defaults = new ResourceLimits();
		int maximumFile = (int)ReadNumber(configuration, "MaximumFileBytes", defaults.MaximumFileBytes, 16L * 1024 * 1024);
		long ownerBudget = ReadNumber(configuration, "OwnerBudgetBytes", defaults.OwnerBudgetBytes, 1536L * 1024 * 1024);
		long globalBudget = ReadNumber(configuration, "GlobalBudgetBytes", defaults.GlobalBudgetBytes, 1536L * 1024 * 1024);
		if (ownerBudget > globalBudget)
			throw new InvalidOperationException("The owner resource budget cannot exceed the global resource budget.");
		return new ResourceLimits(maximumFile, ownerBudget, globalBudget);
	}

	private static long ReadNumber(IConfiguration configuration, string name, long fallback, long maximum)
	{
		string? value = configuration["ResourceLimits:" + name];
		if (value is null)
			return fallback;
		if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) || parsed < 1 || parsed > maximum)
			throw new InvalidOperationException("A ResourceLimits setting is outside its safe SQL Basic range: " + name);
		return parsed;
	}
}
