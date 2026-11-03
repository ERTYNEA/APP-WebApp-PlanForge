namespace PlanForge.Domain.Planning;

public sealed class Phase
{
	public Phase(string title)
	{
		Title = title;
	}

	public IReadOnlyList<Subphase> Subphases { get; } = [new Subphase()];
	public string Title { get; }
}