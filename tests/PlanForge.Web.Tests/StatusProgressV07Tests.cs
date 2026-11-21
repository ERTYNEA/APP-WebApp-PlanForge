namespace PlanForge.Web.Tests;

using Bunit;
using PlanForge.Contracts.Planning;
using PlanForge.Web.Components.Shared;
using System.Globalization;

public sealed class StatusProgressV07Tests
{
	[Theory]
	[InlineData(0, 0, 0, 0, "0/0 · 0%")]
	[InlineData(0, 0, 0, 1, "1/1 · 100%")]
	[InlineData(8, 7, 3, 10, "10/28 · 36%")]
	[InlineData(8, 9, 3, 12, "12/32 · 38%")]
	[InlineData(100000, 200000, 300000, 400000, "400000/1000000 · 40%")]
	public void Counts_AreExactOrderedAccessibleAndCultureSafe(int pending, int active, int blocked, int completed, string ratio)
	{
		using var context = new ComponentTestContext(_ => throw new InvalidOperationException("No API calls"));
		CultureInfo previous = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new("es-ES");
			var page = context.Render<StatusProgress>(parameters => parameters.Add(component => component.Title, "Steps").Add(component => component.Counts, new StatusCounts(pending, active, blocked, completed)));
			Assert.Equal(ratio, page.Find(".status-ratio").TextContent);
			Assert.Equal(new[] { pending, active, blocked, completed }.Select(count => count.ToString(CultureInfo.InvariantCulture)), page.FindAll(".status-segment").Select(element => element.GetAttribute("data-count")));
			Assert.Equal(new[] { "Pending", "In progress", "Blocked", "Completed" }, page.FindAll("dt").Select(element => element.TextContent));
			Assert.All(page.FindAll(".status-segment"), element => Assert.Equal("flex-grow:" + element.GetAttribute("data-count"), element.GetAttribute("style")));
			Assert.Contains("Steps:", page.Find("[role=img]").GetAttribute("aria-label"));
			Assert.DoesNotContain("NaN", page.Markup);
		}
		finally { CultureInfo.CurrentCulture = previous; }
	}
}
