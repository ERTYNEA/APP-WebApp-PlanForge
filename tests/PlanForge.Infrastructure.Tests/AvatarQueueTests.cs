namespace PlanForge.Infrastructure.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Database;

public sealed class AvatarQueueTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DeletingAgent_CancelsItsAvatarWithoutOrphanedWorkOrResurrectionAsync(bool processing)
	{
		await using var fixture = new AvatarFixture();
		Guid agentId = Guid.NewGuid();
		await using (var setup = fixture.CreateDbContext())
		{
			setup.Agents.Add(new PlanForge.Domain.Agents.Agent { Id = agentId, OwnerUserId = fixture.OwnerId, Name = "Owned QA" });
			await setup.SaveChangesAsync();
		}
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, agentId);
		await fixture.Store.AcceptAsync(admission.Id, [1, 2], "image/png");
		var work = processing ? await fixture.Store.ClaimAsync(Guid.NewGuid()) : null;
		await using (var deletion = fixture.CreateDbContext())
			await new PlanForge.Infrastructure.Agents.EfAgentStore(deletion).DeleteAsync(fixture.OwnerId, agentId, default);
		await using var after = fixture.CreateDbContext();
		var job = await after.AvatarOptimizationJobs.SingleAsync();
		Assert.False(job.IsCurrent);
		Assert.Equal(processing ? AvatarJobStatus.Cancelling : AvatarJobStatus.Cancelled, job.Status);
		if (work is not null)
		{
			Assert.False(await fixture.Store.CompleteAsync(work, [3]));
			await fixture.Store.ReleaseAsync(work);
		}
		Assert.False(await fixture.Store.HasOutstandingAsync());
		Assert.Empty(await after.Agents.ToListAsync());
	}

	[Fact]
	public async Task Heartbeat_ExtendsLeaseButRejectsCancelledGenerationAsync()
	{
		await using var fixture = new AvatarFixture();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(admission.Id, [1], "image/png");
		var work = await fixture.Store.ClaimAsync(Guid.NewGuid());
		Assert.NotNull(work);
		fixture.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.True(await fixture.Store.RenewAsync(work));
		fixture.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.Null(await fixture.Store.ClaimAsync(Guid.NewGuid()));
		await fixture.Store.DeleteAsync(fixture.OwnerId, null);
		Assert.False(await fixture.Store.RenewAsync(work));
		await fixture.Store.ReleaseAsync(work);
	}

	[Fact]
	public async Task ExpiredWorker_RecoversWithNewFenceAndRejectsOldPublicationAsync()
	{
		await using var fixture = new AvatarFixture();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(admission.Id, [1, 2], "image/png");
		var first = await fixture.Store.ClaimAsync(Guid.NewGuid());
		Assert.NotNull(first);
		fixture.Clock.Advance(TimeSpan.FromSeconds(31));
		var recovered = await fixture.Store.ClaimAsync(Guid.NewGuid());
		Assert.NotNull(recovered);
		Assert.True(recovered.Fence > first.Fence);
		Assert.False(await fixture.Store.CompleteAsync(first, [8]));
		Assert.True(await fixture.Store.CompleteAsync(recovered, [9]));
	}

	[Fact]
	public async Task ExpiredCancellation_CleansRetainedBytesWithoutNewUploadAsync()
	{
		await using var fixture = new AvatarFixture();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(admission.Id, [1, 2], "image/png");
		Assert.NotNull(await fixture.Store.ClaimAsync(Guid.NewGuid()));
		await fixture.Store.DeleteAsync(fixture.OwnerId, null);
		fixture.Clock.Advance(TimeSpan.FromSeconds(31));
		Assert.Null(await fixture.Store.ClaimAsync(Guid.NewGuid()));
		await using var database = fixture.CreateDbContext();
		var job = await database.AvatarOptimizationJobs.SingleAsync();
		Assert.Null(job.RetainedBytes);
		Assert.Equal(AvatarJobStatus.Cancelled, job.Status);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Delete_InvalidatesPendingOrActiveAndNeverResurrectsAsync(bool processing)
	{
		await using var fixture = new AvatarFixture();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(admission.Id, [1, 2], "image/png");
		var work = processing ? await fixture.Store.ClaimAsync(Guid.NewGuid()) : null;
		Assert.False((await fixture.Store.DeleteAsync(fixture.OwnerId, null)).HasAvatar);
		if (work is not null)
		{
			Assert.False(await fixture.Store.CompleteAsync(work, [5]));
			await fixture.Store.ReleaseAsync(work);
		}
		await using var database = fixture.CreateDbContext();
		Assert.Null((await database.Users.SingleAsync()).AvatarBytes);
		var job = await database.AvatarOptimizationJobs.SingleAsync();
		Assert.Equal(AvatarJobStatus.Cancelled, job.Status);
		Assert.Null(job.RetainedBytes);
		Assert.False(job.IsCurrent);
	}

	[Fact]
	public async Task Failure_RetriesThreeTimesThenClearsCurrentWithoutRestoringOldAsync()
	{
		await using var fixture = new AvatarFixture();
		await using (var old = fixture.CreateDbContext())
		{
			var user = await old.Users.SingleAsync();
			user.AvatarBytes = [9]; user.AvatarContentType = "image/webp";
			await old.SaveChangesAsync();
		}
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(admission.Id, [1, 2], "image/png");
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			var work = await fixture.Store.ClaimAsync(Guid.NewGuid());
			Assert.NotNull(work);
			await fixture.Store.FailAsync(work);
			await using var database = fixture.CreateDbContext();
			var job = await database.AvatarOptimizationJobs.SingleAsync();
			Assert.Equal(attempt, job.Attempts);
			Assert.Equal(attempt == 3 ? AvatarJobStatus.Failed : AvatarJobStatus.Retry, job.Status);
			Assert.Equal(attempt != 3, (await database.Users.SingleAsync()).AvatarBytes is not null);
			fixture.Clock.Advance(TimeSpan.FromMinutes(1));
		}
		Assert.Null(await fixture.Store.ClaimAsync(Guid.NewGuid()));
	}

	[Fact]
	public async Task ActiveReplacement_CancelsOldGenerationAndMovesNewWorkBehindOtherTargetsAsync()
	{
		await using var fixture = new AvatarFixture();
		var first = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(first.Id, [1, 2, 3], "image/png");
		var work = await fixture.Store.ClaimAsync(Guid.NewGuid());
		Assert.NotNull(work);
		Guid agentId = Guid.NewGuid();
		await using (var setup = fixture.CreateDbContext())
		{
			setup.Agents.Add(new PlanForge.Domain.Agents.Agent { Id = agentId, OwnerUserId = fixture.OwnerId, Name = "QA" });
			await setup.SaveChangesAsync();
		}
		var other = await fixture.Store.PrepareAsync(fixture.OwnerId, agentId);
		await fixture.Store.AcceptAsync(other.Id, [4], "image/png");
		var next = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		var accepted = await fixture.Store.AcceptAsync(next.Id, [5, 6], "image/png");
		Assert.False(await fixture.Store.CompleteAsync(work, [9]));
		await using var database = fixture.CreateDbContext();
		var jobs = await database.AvatarOptimizationJobs.OrderBy(job => job.Sequence).ToListAsync();
		Assert.Equal(3, jobs.Count);
		Assert.Equal(AvatarJobStatus.Cancelling, jobs[0].Status);
		Assert.False(jobs[0].IsCurrent);
		Assert.Equal(new byte[] { 1, 2, 3 }, jobs[0].RetainedBytes);
		Assert.Equal(agentId, jobs[1].AgentId);
		Assert.Equal(accepted.Version, jobs[2].Generation);
		Assert.Equal(new byte[] { 5, 6 }, (await database.Users.SingleAsync()).AvatarBytes);
	}

	[Fact]
	public async Task Worker_ClaimsFifoOnceAndAtomicallyReplacesOriginalAsync()
	{
		await using var fixture = new AvatarFixture();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		var accepted = await fixture.Store.AcceptAsync(admission.Id, [1, 2, 3], "image/png");
		var work = await fixture.Store.ClaimAsync(Guid.NewGuid());
		Assert.NotNull(work);
		Assert.Equal(new byte[] { 1, 2, 3 }, work.Bytes);
		Assert.Null(await fixture.Store.ClaimAsync(Guid.NewGuid()));
		Assert.True(await fixture.Store.CompleteAsync(work, [4, 5]));
		Assert.False(await fixture.Store.CompleteAsync(work, [6]));
		await using var database = fixture.CreateDbContext();
		var current = await database.Users.SingleAsync();
		Assert.False(current.AvatarTemporary);
		Assert.NotEqual(accepted.Version, current.AvatarGeneration);
		Assert.Equal("image/webp", current.AvatarContentType);
		Assert.Equal(new byte[] { 4, 5 }, current.AvatarBytes);
		var job = await database.AvatarOptimizationJobs.SingleAsync();
		Assert.Equal(AvatarJobStatus.Completed, job.Status);
		Assert.False(job.IsCurrent);
		Assert.Null(job.RetainedBytes);
		Assert.Equal(1, job.Attempts);
	}

	[Fact]
	public async Task Quota_AggregatesHumanAndAllAgentsAndRejectsWithoutChangingCurrentAsync()
	{
		await using var fixture = new AvatarFixture();
		await using (var setup = fixture.CreateDbContext())
		{
			for (int index = 0; index < 10; index++)
				setup.Agents.Add(new PlanForge.Domain.Agents.Agent { OwnerUserId = fixture.OwnerId, Name = "QA " + index, AvatarBytes = new byte[4 * 1024 * 1024], AvatarTemporary = true, AvatarContentType = "image/png" });
			await setup.SaveChangesAsync();
		}
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		Assert.Equal(413, (await Assert.ThrowsAsync<PlanForge.Application.Common.PlanForgeException>(() => fixture.Store.AcceptAsync(admission.Id, [1], "image/png"))).StatusCode);
		await using var after = fixture.CreateDbContext();
		Assert.Null((await after.Users.SingleAsync()).AvatarBytes);
		Assert.Empty(await after.AvatarOptimizationJobs.ToListAsync());
		Assert.False((await after.AvatarAdmissions.SingleAsync()).Accepted);
	}

	[Fact]
	public async Task Quota_PendingReplacementUsesNetBytesAtInclusiveLimitAsync()
	{
		await using var fixture = new AvatarFixture();
		await using (var setup = fixture.CreateDbContext())
		{
			for (int index = 0; index < 9; index++)
				setup.Agents.Add(new PlanForge.Domain.Agents.Agent { OwnerUserId = fixture.OwnerId, Name = "QA " + index, AvatarBytes = new byte[4 * 1024 * 1024], AvatarTemporary = true });
			await setup.SaveChangesAsync();
		}
		var first = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(first.Id, new byte[4 * 1024 * 1024], "image/png");
		var next = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		Assert.True((await fixture.Store.AcceptAsync(next.Id, new byte[4 * 1024 * 1024], "image/png")).HasAvatar);
	}

	[Fact]
	public async Task PendingReplacement_KeepsFifoTurnAndDestroysOldOriginalAsync()
	{
		await using var fixture = new AvatarFixture();
		var first = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(first.Id, [1, 2, 3], "image/png");
		await using var before = fixture.CreateDbContext();
		long sequence = (await before.AvatarOptimizationJobs.SingleAsync()).Sequence;
		var next = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await fixture.Store.AcceptAsync(next.Id, [4, 5], "image/jpeg");
		await using var after = fixture.CreateDbContext();
		var job = await after.AvatarOptimizationJobs.SingleAsync();
		Assert.Equal(sequence, job.Sequence);
		Assert.Equal(new byte[] { 4, 5 }, (await after.Users.SingleAsync()).AvatarBytes);
		Assert.Null(job.RetainedBytes);
	}

	[Fact]
	public async Task Accept_PersistsOfficialOriginalAndOneFifoJobAsync()
	{
		await using var fixture = new AvatarFixture();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		var accepted = await fixture.Store.AcceptAsync(admission.Id, [1, 2, 3], "image/png");
		Assert.True(accepted.HasAvatar);
		await using var database = fixture.CreateDbContext();
		var user = await database.Users.SingleAsync(user => user.Id == fixture.OwnerId);
		Assert.Equal(new byte[] { 1, 2, 3 }, user.AvatarBytes);
		Assert.True(user.AvatarTemporary);
		var job = await database.AvatarOptimizationJobs.SingleAsync();
		Assert.Equal(AvatarJobStatus.Pending, job.Status);
		Assert.Equal(user.AvatarGeneration, job.Generation);
		Assert.Null(job.RetainedBytes);
		Assert.True((await database.AvatarAdmissions.SingleAsync()).Accepted);
	}
}

internal sealed class AvatarFixture : IDbContextFactory<PlanForgeDbContext>, IAsyncDisposable
{
	private readonly SqliteConnection keeper;
	private readonly DbContextOptions<PlanForgeDbContext> options;
	public Guid OwnerId { get; } = Guid.NewGuid();
	public EfAvatarStore Store { get; }
	public AvatarTestClock Clock { get; } = new();

	public AvatarFixture()
	{
		string connectionString = "Data Source=avatar-" + Guid.NewGuid() + ";Mode=Memory;Cache=Shared";
		keeper = new SqliteConnection(connectionString);
		keeper.Open();
		options = new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlite(connectionString).Options;
		using var database = CreateDbContext();
		database.Database.EnsureCreated();
		database.Users.Add(new UserAccount { Id = OwnerId, Email = "avatar@example.test", NormalizedEmail = "AVATAR@EXAMPLE.TEST", DisplayName = "QA", PasswordHash = "isolated-fixture-not-a-credential" });
		database.SaveChanges();
		Store = new EfAvatarStore(this, Clock);
	}

	public PlanForgeDbContext CreateDbContext() => new(options);
	public ValueTask DisposeAsync() => keeper.DisposeAsync();
}

internal sealed class AvatarTestClock : TimeProvider
{
	private DateTimeOffset now = DateTimeOffset.UtcNow;
	public override DateTimeOffset GetUtcNow() => now;
	public void Advance(TimeSpan interval) => now += interval;
}
