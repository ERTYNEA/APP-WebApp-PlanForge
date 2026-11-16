namespace PlanForge.Web.State;

public sealed record ToastNotification(string Message, NotificationKind Kind, long? EventId = null);
