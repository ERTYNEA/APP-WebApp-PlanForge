namespace PlanForge.Api.Tests;

using Microsoft.Extensions.Configuration;
using PlanForge.Api.Library;

public sealed class ResourceLimitConfigurationTests
{
	[Fact]
	public void Read_WhenConfigured_UsesScopedLimitsAndRejectsUnsafeSqlBasicBudget()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ResourceLimits:MaximumFileBytes"] = "1024",
			["ResourceLimits:OwnerBudgetBytes"] = "4096",
			["ResourceLimits:GlobalBudgetBytes"] = "16384"
		}).Build();
		var limits = ResourceLimitConfiguration.Read(configuration);
		Assert.Equal(1024, limits.MaximumFileBytes);
		Assert.Equal(4096, limits.OwnerBudgetBytes);
		configuration["ResourceLimits:GlobalBudgetBytes"] = "2147483648";
		Assert.Throws<InvalidOperationException>(() => ResourceLimitConfiguration.Read(configuration));
	}
}
