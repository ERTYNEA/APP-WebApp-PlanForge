namespace PlanForge.Api.Avatars;

/// <summary>Reports whether startup proved the existing storage can durably wake and recover avatar work.</summary>
public interface IAvatarQueueReadiness
{
	bool IsReady { get; }
}
