namespace PlanForge.Web.State;

/// <summary>Owns short-lived notifications for one Blazor circuit, independently of page lifetimes.</summary>
public sealed class NotificationState : IDisposable
{
	private readonly object sync = new();
	private readonly List<(ToastNotification Notification, DateTimeOffset Expires, long Sequence)> entries = [];
	private readonly TimeProvider clock;
	private readonly ITimer timer;
	private bool disposed;
	private long publicationVersion, announcedVersion;
	private readonly List<Guid> modalHosts = [];

	public Guid? ActiveHost { get { lock (sync) return modalHosts.Count == 0 ? null : modalHosts[^1]; } }

	public Guid RegisterModal()
	{
		Guid host = Guid.NewGuid();
		lock (sync) modalHosts.Add(host);
		Changed?.Invoke();
		return host;
	}

	public void UnregisterModal(Guid host)
	{
		lock (sync) modalHosts.Remove(host);
		Changed?.Invoke();
	}

	public NotificationState(TimeProvider? clock = null)
	{
		this.clock = clock ?? TimeProvider.System;
		timer = this.clock.CreateTimer(_ => Expire(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
	}

	/// <summary>Returns only results not yet rendered by an active host; handoffs never replay old announcements.</summary>
	public (long Version, string Message) ReadAnnouncement(Guid? host)
	{
		lock (sync)
			return ActiveHost == host ? (publicationVersion, string.Join(". ", entries.Where(entry => entry.Sequence > announcedVersion).Select(entry => entry.Notification.Message))) : (0, "");
	}

	public void AcknowledgeAnnouncement(long version)
	{
		lock (sync) announcedVersion = Math.Max(announcedVersion, Math.Min(version, publicationVersion));
	}

	public event Action? Changed;
	public IReadOnlyList<ToastNotification> Items
	{
		get { lock (sync) return entries.Select(entry => entry.Notification).ToArray(); }
	}

	public void Dispose()
	{
		lock (sync)
		{
			disposed = true;
			entries.Clear();
			timer.Dispose();
			Changed = null;
		}
	}

	public void Publish(string message, NotificationKind kind, long? eventId = null)
	{
		lock (sync)
		{
			if (disposed) return;
			var notification = new ToastNotification(UiText.Get(message), kind, eventId);
			long sequence = entries.FirstOrDefault(entry => entry.Notification == notification).Sequence;
			if (sequence == 0) sequence = ++publicationVersion;
			entries.RemoveAll(entry => entry.Notification == notification);
			entries.Add((notification, clock.GetUtcNow().AddSeconds(6), sequence));
			if (entries.Count > 4) entries.RemoveAt(0);
			Schedule();
		}
		Changed?.Invoke();
	}

	private void Expire()
	{
		lock (sync)
		{
			if (disposed) return;
			entries.RemoveAll(entry => entry.Expires <= clock.GetUtcNow());
			Schedule();
		}
		Changed?.Invoke();
	}

	private void Schedule()
	{
		TimeSpan due = entries.Count == 0 ? Timeout.InfiniteTimeSpan : entries.Min(entry => entry.Expires) - clock.GetUtcNow();
		timer.Change(due < TimeSpan.Zero && due != Timeout.InfiniteTimeSpan ? TimeSpan.Zero : due, Timeout.InfiniteTimeSpan);
	}
}
