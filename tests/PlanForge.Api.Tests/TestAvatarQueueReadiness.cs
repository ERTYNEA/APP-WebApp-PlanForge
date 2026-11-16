namespace PlanForge.Api.Tests;

using PlanForge.Api.Avatars;

internal sealed record TestAvatarQueueReadiness(bool IsReady) : IAvatarQueueReadiness;
