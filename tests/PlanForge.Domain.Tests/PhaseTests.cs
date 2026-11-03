namespace PlanForge.Domain.Tests;

using PlanForge.Domain.Planning;

public sealed class PhaseTests
{
	[Fact]
	public void Constructor_WhenCreated_HasRealDefaultSubphase()
	{
		var phase = new Phase("Infrastructure");

		Subphase subphase = Assert.Single(phase.Subphases);
		Assert.Equal("Default subphase", subphase.Title);
	}
}