namespace PlanForge.Application.Accounts;

/// <summary>Signals loss of the exact distributed session that owns conversion exclusion.</summary>
public interface IAvatarConversionLease : IAsyncDisposable
{
	CancellationToken LostToken { get; }
}
