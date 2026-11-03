namespace PlanForge.Api.Tests;

using PlanForge.Api.Http;
using PlanForge.Application.Common;

public sealed class RequestBudgetTests
{
	[Fact]
	public void Acquire_WhenWindowExhausted_Returns429AndRecoversAfterWindow()
	{
		var clock = new ManualClock();
		var budget = new RequestBudget(clock);
		budget.Acquire("login", 2, TimeSpan.FromMinutes(1));
		budget.Acquire("login", 2, TimeSpan.FromMinutes(1));

		PlanForgeException error = Assert.Throws<PlanForgeException>(() => budget.Acquire("login", 2, TimeSpan.FromMinutes(1)));

		Assert.Equal(429, error.StatusCode);
		clock.Now = clock.Now.AddMinutes(1);
		budget.Acquire("login", 2, TimeSpan.FromMinutes(1));
	}

	private sealed class ManualClock : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-11-03T12:00:00Z");
		public override DateTimeOffset GetUtcNow() => Now;
	}
}
