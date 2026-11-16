namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Domain.Accounts;

public sealed class AvatarAtomicityTests
{
	[Fact]
	public async Task AcceptanceFailureAfterPhotoWrite_RollsBackPhotoQueueAndReceiptTogetherAsync()
	{
		await using var fixture = new AvatarFixture();
		await using (var setup = fixture.CreateDbContext())
		{
			await setup.Users.ExecuteUpdateAsync(setters => setters.SetProperty(user => user.AvatarBytes, new byte[] { 9 }).SetProperty(user => user.AvatarContentType, "image/webp"));
			await setup.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_avatar_job BEFORE INSERT ON AvatarOptimizationJobs BEGIN SELECT RAISE(ABORT, 'isolated rollback'); END;");
		}
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Store.AcceptAsync(admission.Id, [1, 2, 3], "image/png"));
		await using var after = fixture.CreateDbContext();
		Assert.Equal(new byte[] { 9 }, (await after.Users.SingleAsync()).AvatarBytes);
		Assert.False((await after.Users.SingleAsync()).AvatarTemporary);
		Assert.Empty(await after.AvatarOptimizationJobs.ToListAsync());
		Assert.False((await after.AvatarAdmissions.SingleAsync()).Accepted);
		Assert.Equal(0, (await after.AvatarQueueStates.SingleAsync()).NextSequence);
	}

	[Fact]
	public async Task PublicationFailureBetweenActivationAndJobCommit_KeepsValidOriginalUntilAtomicRetryAsync()
	{
		await using var fixture = new AvatarFixture();
		var admission = await fixture.Store.PrepareAsync(fixture.OwnerId, null);
		var original = await fixture.Store.AcceptAsync(admission.Id, [1, 2, 3], "image/png");
		var work = await fixture.Store.ClaimAsync(Guid.NewGuid());
		Assert.NotNull(work);
		await using (var setup = fixture.CreateDbContext())
			await setup.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_avatar_completion BEFORE UPDATE ON AvatarOptimizationJobs WHEN NEW.Status = 4 BEGIN SELECT RAISE(ABORT, 'isolated rollback'); END;");
		await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Store.CompleteAsync(work, [5, 6]));
		await using (var after = fixture.CreateDbContext())
		{
			var user = await after.Users.SingleAsync();
			Assert.Equal(new byte[] { 1, 2, 3 }, user.AvatarBytes);
			Assert.True(user.AvatarTemporary);
			Assert.Equal(original.Version, user.AvatarGeneration);
			Assert.Equal(AvatarJobStatus.Processing, (await after.AvatarOptimizationJobs.SingleAsync()).Status);
			await after.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_avatar_completion;");
		}
		Assert.True(await fixture.Store.CompleteAsync(work, [5, 6]));
		await using var completed = fixture.CreateDbContext();
		Assert.Equal(new byte[] { 5, 6 }, (await completed.Users.SingleAsync()).AvatarBytes);
		Assert.False((await completed.Users.SingleAsync()).AvatarTemporary);
		Assert.Equal(AvatarJobStatus.Completed, (await completed.AvatarOptimizationJobs.SingleAsync()).Status);
		Assert.False(await fixture.Store.HasOutstandingAsync());
	}
}
