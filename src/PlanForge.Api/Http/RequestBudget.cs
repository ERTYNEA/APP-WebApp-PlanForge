namespace PlanForge.Api.Http;

public sealed class RequestBudget(TimeProvider timeProvider)
{
	private readonly Dictionary<string, (DateTimeOffset ExpiresAt, int Count)> windows = [];
	private readonly object sync = new();

	public void Acquire(string key, int limit, TimeSpan window)
	{
		lock (sync)
		{
			DateTimeOffset now = timeProvider.GetUtcNow();
			if (!windows.TryGetValue(key, out var current) || current.ExpiresAt <= now)
				current = (now.Add(window), 0);
			if (current.Count >= limit)
				throw new PlanForge.Application.Common.PlanForgeException(429, "Too many requests. Wait before trying again.");
			windows[key] = (current.ExpiresAt, current.Count + 1);
		}
	}
}
