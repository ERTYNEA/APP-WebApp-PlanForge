namespace PlanForge.Infrastructure.Accounts;

/// <summary>Leaves the broker-owned input available for a delayed retry while SQL still has eligible or preparing work.</summary>
public sealed class AvatarWakeDeferredException() : Exception("Avatar work remains durably queued for retry.");
