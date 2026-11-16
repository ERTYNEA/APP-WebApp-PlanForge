namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Domain.Accounts;

public sealed class AvatarSignalTests
{
	private static readonly byte[] image = ImageTestData.Raster(SkiaSharp.SKEncodedImageFormat.Png);

	[Fact]
	public async Task Upload_RejectsImageBombBeforePreparingOrSendingWakeAsync()
	{
		await using var fixture = new AvatarFixture();
		var signal = new RecordingSignal();
		var service = new AvatarService(fixture.Store, signal, new PlanForge.Infrastructure.Accounts.SkiaAvatarImageValidator());
		var actor = new AuthenticatedActor(fixture.OwnerId, CredentialKind.HumanSession, Guid.NewGuid(), null);
		await Assert.ThrowsAsync<PlanForge.Application.Common.PlanForgeException>(() => service.SetAsync(actor, null, ImageTestData.PngWithDimensions(8192, 8192), "image/png"));
		Assert.Empty(signal.Delays);
		await using var database = fixture.CreateDbContext();
		Assert.Empty(await database.AvatarAdmissions.ToListAsync());
		Assert.Null((await database.Users.SingleAsync()).AvatarBytes);
	}

	[Fact]
	public async Task Delete_RequiresWakeAndConfirmsOnlyClearedBytesAsync()
	{
		await using var fixture = new AvatarFixture();
		var signal = new RecordingSignal();
		var service = new AvatarService(fixture.Store, signal, new PlanForge.Infrastructure.Accounts.SkiaAvatarImageValidator());
		var actor = new AuthenticatedActor(fixture.OwnerId, CredentialKind.HumanSession, Guid.NewGuid(), null);
		await service.SetAsync(actor, null, image, "image/png");
		Assert.False((await service.SetAsync(actor, null, null, null)).HasAvatar);
		Assert.Equal(2, signal.Delays.Count);
		await using var database = fixture.CreateDbContext();
		Assert.Null((await database.Users.SingleAsync()).AvatarBytes);
		Assert.All(await database.AvatarAdmissions.ToListAsync(), receipt => Assert.True(receipt.Accepted));
	}

	[Fact]
	public async Task Delete_ActiveReaderDoesNotConfirmUntilReleaseAsync()
	{
		await using var fixture = new AvatarFixture();
		var service = new AvatarService(fixture.Store, new RecordingSignal(), new PlanForge.Infrastructure.Accounts.SkiaAvatarImageValidator());
		var actor = new AuthenticatedActor(fixture.OwnerId, CredentialKind.HumanSession, Guid.NewGuid(), null);
		await service.SetAsync(actor, null, image, "image/png");
		var work = await fixture.Store.ClaimAsync(Guid.NewGuid());
		Assert.NotNull(work);
		Task<PlanForge.Contracts.Accounts.AvatarState> deletion = service.SetAsync(actor, null, null, null);
		Assert.False(deletion.IsCompleted);
		Assert.True(await fixture.Store.HasRetainedAsync(fixture.OwnerId, null));
		await fixture.Store.ReleaseAsync(work);
		Assert.False((await deletion.WaitAsync(TimeSpan.FromSeconds(3))).HasAvatar);
		Assert.False(await fixture.Store.HasRetainedAsync(fixture.OwnerId, null));
	}

	[Fact]
	public async Task Upload_ConfirmsDurableSignalBeforeCommittingOfficialOriginalAfterWakeAsync()
	{
		await using var fixture = new AvatarFixture();
		var signal = new RecordingSignal();
		var service = new AvatarService(fixture.Store, signal, new PlanForge.Infrastructure.Accounts.SkiaAvatarImageValidator());
		var actor = new AuthenticatedActor(fixture.OwnerId, CredentialKind.HumanSession, Guid.NewGuid(), null);
		var state = await service.SetAsync(actor, null, image, "image/png");
		Assert.True(state.HasAvatar);
		Assert.Single(signal.Delays);
		await using var database = fixture.CreateDbContext();
		Assert.True((await database.AvatarAdmissions.SingleAsync()).Accepted);
		Assert.Equal(image, (await database.Users.SingleAsync()).AvatarBytes);
	}

	[Fact]
	public async Task Upload_SignalFailureRejectsWithoutMutatingPreviousPhotoAsync()
	{
		await using var fixture = new AvatarFixture();
		await using (var database = fixture.CreateDbContext())
		{
			var user = await database.Users.SingleAsync(); user.AvatarBytes = [9]; user.AvatarContentType = "image/webp";
			await database.SaveChangesAsync();
		}
		var signal = new RecordingSignal { Fail = true };
		var service = new AvatarService(fixture.Store, signal, new PlanForge.Infrastructure.Accounts.SkiaAvatarImageValidator());
		var actor = new AuthenticatedActor(fixture.OwnerId, CredentialKind.HumanSession, Guid.NewGuid(), null);
		await Assert.ThrowsAsync<IOException>(() => service.SetAsync(actor, null, image, "image/png"));
		await using var after = fixture.CreateDbContext();
		Assert.Equal(new byte[] { 9 }, (await after.Users.SingleAsync()).AvatarBytes);
		Assert.Empty(await after.AvatarOptimizationJobs.ToListAsync());
		Assert.False((await after.AvatarAdmissions.SingleAsync()).Accepted);
	}
}

internal sealed class RecordingSignal : IAvatarWakeSignal
{
	public bool Fail { get; set; }
	public List<TimeSpan> Delays { get; } = [];
	public List<AvatarWakeReceipt> Deleted { get; } = [];
	public Task<AvatarWakeReceipt> SendAsync(TimeSpan delay, CancellationToken cancellationToken = default)
	{
		if (Fail)
			throw new IOException("Injected transport failure.");
		Delays.Add(delay);
		return Task.FromResult(new AvatarWakeReceipt(Guid.NewGuid().ToString(), "test-pop-receipt"));
	}
	public Task DeleteAsync(AvatarWakeReceipt receipt, CancellationToken cancellationToken = default)
	{
		Deleted.Add(receipt);
		return Task.CompletedTask;
	}
}
