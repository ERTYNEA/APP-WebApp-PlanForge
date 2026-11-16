namespace PlanForge.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;
using PlanForge.Domain.Accounts;
using PlanForge.Domain.Agents;
using PlanForge.Infrastructure.Accounts;
using Xunit.Abstractions;

/// <summary>Supplemental SQLite concurrency evidence, not SQL Server row-lock or cross-process applock proof.</summary>
public sealed class AvatarConcurrencyTests(ITestOutputHelper output)
{
	private const int Mebibyte = 1024 * 1024;

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task LastOwnerSpace_ParallelHumanAndAgentAdmissionAcceptsOnlyOneWithoutPriorDeletionAsync(int iteration)
	{
		await using var fixture = new AvatarConcurrencyFixture();
		Guid targetAgentId = Guid.NewGuid();
		Guid humanVersion = Guid.NewGuid();
		Guid agentVersion = Guid.NewGuid();
		await using (var setup = fixture.CreateDbContext())
		{
			var user = await setup.Users.SingleAsync();
			user.AvatarBytes = [90];
			user.AvatarContentType = "image/webp";
			user.AvatarGeneration = humanVersion;
			setup.Agents.Add(new Agent { Id = targetAgentId, OwnerUserId = fixture.OwnerId, Name = "Candidate", AvatarBytes = [91], AvatarContentType = "image/webp", AvatarGeneration = agentVersion });
			for (int index = 0; index < 9; index++)
				setup.Agents.Add(new Agent { OwnerUserId = fixture.OwnerId, Name = "Temporary " + index, AvatarBytes = new byte[4 * Mebibyte], AvatarTemporary = true, AvatarContentType = "image/png" });
			await setup.SaveChangesAsync();
		}
		EfAvatarStore humanStore = fixture.CreateStore();
		EfAvatarStore agentStore = fixture.CreateStore();
		var humanAdmission = await humanStore.PrepareAsync(fixture.OwnerId, null);
		var agentAdmission = await agentStore.PrepareAsync(fixture.OwnerId, targetAgentId);
		byte[] humanBytes = new byte[4 * Mebibyte];
		byte[] agentBytes = new byte[4 * Mebibyte];
		humanBytes[0] = (byte)(iteration + 1);
		agentBytes[0] = (byte)(iteration + 11);
		fixture.ArmTransactionRace();
		AdmissionResult[] results = await RaceAsync(
			() => AdmitAsync(humanStore, humanAdmission.Id, humanBytes),
			() => AdmitAsync(agentStore, agentAdmission.Id, agentBytes));
		AdmissionResult winner = Assert.Single(results, result => result.State is not null);
		Assert.True(winner.State!.HasAvatar);
		Assert.Equal(413, Assert.Single(results, result => result.Error is not null).Error!.StatusCode);
		Assert.True(fixture.TransactionOverlapVerified);
		await using var after = fixture.CreateDbContext();
		var human = await after.Users.SingleAsync();
		var agent = await after.Agents.SingleAsync(agent => agent.Id == targetAgentId);
		bool humanWon = results[0].State is not null;
		Assert.Equal(humanWon ? humanBytes : new byte[] { 90 }, human.AvatarBytes);
		Assert.Equal(humanWon ? new byte[] { 91 } : agentBytes, agent.AvatarBytes);
		Assert.Equal(humanWon ? winner.State.Version : humanVersion, human.AvatarGeneration);
		Assert.Equal(humanWon ? agentVersion : winner.State.Version, agent.AvatarGeneration);
		Assert.Equal(humanWon, human.AvatarTemporary);
		Assert.Equal(!humanWon, agent.AvatarTemporary);
		Assert.Equal(9, await after.Agents.CountAsync(item => item.Id != targetAgentId && item.AvatarTemporary && item.AvatarBytes!.Length == 4 * Mebibyte));
		long physicalTemporary = await after.Users.Where(item => item.AvatarTemporary).SumAsync(item => (long)item.AvatarBytes!.Length)
			+ await after.Agents.Where(item => item.AvatarTemporary).SumAsync(item => (long)item.AvatarBytes!.Length);
		Assert.Equal(EfAvatarStore.TemporaryOwnerLimit, physicalTemporary);
		var job = Assert.Single(await after.AvatarOptimizationJobs.ToListAsync());
		Assert.True(job.IsCurrent);
		Assert.Equal(winner.State.Version, job.Generation);
		Assert.Null(job.RetainedBytes);
		Assert.Single(await after.AvatarAdmissions.Where(item => item.Accepted).ToListAsync());
		Assert.Single(await after.AvatarAdmissions.Where(item => !item.Accepted).ToListAsync());
		Assert.True(fixture.ContextCount >= 6);
		output.WriteLine($"Quota iteration={iteration}; winner={(humanWon ? "human" : "agent")}; temporaryBytes={physicalTemporary}; admitted=1 rejected413=1; transactionOverlapVerified=true.");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PendingSameAvatar_ParallelReplacementsKeepOneTurnAndLastCommittedGenerationAsync(bool isAgent)
	{
		await using var fixture = new AvatarConcurrencyFixture();
		Guid? agentId = await AddTargetAsync(fixture, isAgent);
		EfAvatarStore firstStore = fixture.CreateStore();
		EfAvatarStore secondStore = fixture.CreateStore();
		var original = await firstStore.PrepareAsync(fixture.OwnerId, agentId);
		await firstStore.AcceptAsync(original.Id, [1], "image/png");
		Guid jobId;
		long sequence;
		await using (var before = fixture.CreateDbContext())
		{
			var job = await before.AvatarOptimizationJobs.SingleAsync();
			jobId = job.Id;
			sequence = job.Sequence;
		}
		var first = await firstStore.PrepareAsync(fixture.OwnerId, agentId);
		var second = await secondStore.PrepareAsync(fixture.OwnerId, agentId);
		fixture.ArmTransactionRace();
		AvatarState[] results = await RaceAsync(
			() => firstStore.AcceptAsync(first.Id, [2, 2], "image/png"),
			() => secondStore.AcceptAsync(second.Id, [3, 3, 3], "image/jpeg"));
		Assert.True(fixture.TransactionOverlapVerified);
		Guid[] commits = await fixture.ReadCommitsAsync(agentId ?? fixture.OwnerId);
		Assert.Equal(3, commits.Length);
		Assert.Equal(results.Select(state => state.Version).Order(), commits.TakeLast(2).Order());
		int winningIndex = Array.FindIndex(results, state => state.Version == commits[^1]);
		Assert.InRange(winningIndex, 0, 1);
		await using var after = fixture.CreateDbContext();
		var current = Assert.Single(await after.AvatarOptimizationJobs.ToListAsync());
		Assert.Equal(jobId, current.Id);
		Assert.Equal(sequence, current.Sequence);
		Assert.Equal(commits[^1], current.Generation);
		Assert.Equal(AvatarJobStatus.Pending, current.Status);
		Assert.True(current.IsCurrent);
		Assert.Equal(0, current.Attempts);
		Assert.Null(current.RetainedBytes);
		var photo = await ReadPhotoAsync(fixture, agentId);
		Assert.Equal(commits[^1], photo.Generation);
		Assert.Equal(winningIndex == 0 ? new byte[] { 2, 2 } : new byte[] { 3, 3, 3 }, photo.Bytes);
		Assert.Equal(winningIndex == 0 ? "image/png" : "image/jpeg", photo.ContentType);
		Assert.True(photo.Temporary);
		Assert.All(await after.AvatarAdmissions.ToListAsync(), receipt => Assert.True(receipt.Accepted));
		output.WriteLine($"Pending replacement isAgent={isAgent}; winner={winningIndex}; committedUpdates={commits.Length}; jobs=1; FIFOSequence={sequence}; transactionOverlapVerified=true.");
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task DeleteUploadRace_LastRelationalCommitWinsAndOldWorkerCannotPublishAsync(bool isAgent, bool processing)
	{
		await using var fixture = new AvatarConcurrencyFixture();
		Guid? agentId = await AddTargetAsync(fixture, isAgent);
		EfAvatarStore uploadStore = fixture.CreateStore();
		EfAvatarStore deleteStore = fixture.CreateStore();
		var original = await uploadStore.PrepareAsync(fixture.OwnerId, agentId);
		await uploadStore.AcceptAsync(original.Id, [1], "image/png");
		AvatarWork? stale = processing ? await uploadStore.ClaimAsync(Guid.NewGuid()) : null;
		if (processing)
			Assert.NotNull(stale);
		var upload = await uploadStore.PrepareAsync(fixture.OwnerId, agentId);
		var delete = await deleteStore.PrepareAsync(fixture.OwnerId, agentId);
		fixture.ArmTransactionRace();
		AvatarState[] results = await RaceAsync(
			() => uploadStore.AcceptAsync(upload.Id, [2, 3], "image/jpeg"),
			() => deleteStore.DeleteAsync(fixture.OwnerId, agentId, admissionId: delete.Id));
		Assert.True(fixture.TransactionOverlapVerified);
		Guid[] commits = await fixture.ReadCommitsAsync(agentId ?? fixture.OwnerId);
		Assert.Equal(3, commits.Length);
		Assert.Equal(results.Select(state => state.Version).Order(), commits.TakeLast(2).Order());
		bool uploadWon = commits[^1] == results[0].Version;
		if (stale is not null)
		{
			Assert.False(await fixture.CreateStore().CompleteAsync(stale, [99]));
			await fixture.CreateStore().ReleaseAsync(stale);
		}
		var photo = await ReadPhotoAsync(fixture, agentId);
		Assert.Equal(commits[^1], photo.Generation);
		Assert.Equal(uploadWon ? new byte[] { 2, 3 } : null, photo.Bytes);
		Assert.Equal(uploadWon ? "image/jpeg" : null, photo.ContentType);
		Assert.Equal(uploadWon, photo.Temporary);
		await using var after = fixture.CreateDbContext();
		var jobs = await after.AvatarOptimizationJobs.ToListAsync();
		Assert.Equal(uploadWon ? 1 : 0, jobs.Count(job => job.IsCurrent));
		Assert.All(jobs, job => Assert.Null(job.RetainedBytes));
		Assert.DoesNotContain(jobs, job => job.Status is AvatarJobStatus.Processing or AvatarJobStatus.Cancelling);
		if (uploadWon)
			Assert.Equal(commits[^1], Assert.Single(jobs, job => job.IsCurrent).Generation);
		Assert.All(await after.AvatarAdmissions.ToListAsync(), receipt => Assert.True(receipt.Accepted));
		output.WriteLine($"Delete/upload isAgent={isAgent} processing={processing}; lastCommit={(uploadWon ? "upload" : "delete")}; currentJobs={jobs.Count(job => job.IsCurrent)}; retainedBytes=0; stalePublication=false; transactionOverlapVerified=true.");
	}

	[Fact]
	public async Task TwoWorkers_DistinctSqlLockInstancesExcludeConverterAndPersistContenderSuccessorAsync()
	{
		await using var fixture = new AvatarConcurrencyFixture();
		Guid? agentId = await AddTargetAsync(fixture, true);
		byte[] humanBytes = ImageTestData.Raster(SkiaSharp.SKEncodedImageFormat.Png);
		byte[] agentBytes = ImageTestData.Raster(SkiaSharp.SKEncodedImageFormat.Jpeg);
		await AcceptImageAsync(fixture, null, humanBytes, "image/png");
		await AcceptImageAsync(fixture, agentId, agentBytes, "image/jpeg");
		var validator = new SkiaAvatarImageValidator();
		var converter = new GatedConverter(validator);
		var firstSignal = new AvatarConcurrencySignal(fixture.ConnectionString);
		var secondSignal = new AvatarConcurrencySignal(fixture.ConnectionString);
		var firstLock = new SqlAvatarConversionLock(fixture);
		var secondLock = new SqlAvatarConversionLock(fixture);
		Assert.NotSame(firstLock, secondLock);
		using var host = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		Task first = Task.Run(() => new AvatarWorker(fixture.CreateStore(), firstSignal, firstLock, converter, validator).DrainAsync(host.Token));
		try
		{
			await converter.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
			await using (IAsyncDisposable? contender = await secondLock.TryAcquireAsync(host.Token))
				Assert.Null(contender);
			await new AvatarWorker(fixture.CreateStore(), secondSignal, secondLock, converter, validator).DrainAsync(host.Token).WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal(1, converter.Calls);
			Assert.Equal(1, converter.Active);
			// Fresh SQL connections, not an in-memory list, observe both future recovery records.
			var pending = await new AvatarConcurrencySignal(fixture.ConnectionString).ReadPendingAsync();
			Assert.Equal(2, pending.Length);
			Assert.All(pending, wake => Assert.Equal(TimeSpan.FromSeconds(10), wake.Delay));
			converter.Release.TrySetResult();
			await first.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal(2, converter.Calls);
			Assert.Equal(1, converter.MaximumActive);
			Assert.Equal(0, converter.Active);
			await using (IAsyncDisposable? released = await secondLock.TryAcquireAsync(host.Token))
				Assert.NotNull(released);
			Assert.Equal(new[] { humanBytes, agentBytes }, converter.Inputs.ToArray());
			var successor = Assert.Single(await secondSignal.ReadPendingAsync());
			// A later delivery on a reconstructed worker is harmless even after another worker drained FIFO.
			await new AvatarWorker(fixture.CreateStore(), secondSignal, new SqlAvatarConversionLock(fixture), converter, validator).DrainAsync(host.Token);
			await secondSignal.DeleteAsync(successor.Receipt);
			Assert.Empty(await secondSignal.ReadPendingAsync());
			Assert.Equal(2, converter.Calls);
			await AssertDrainedAsync(fixture, 2);
			output.WriteLine($"Distinct lock objects=2; worker wake records before release=2; maximumConverters={converter.MaximumActive}; completedJobs=2; pendingWakeRecords=0.");
		}
		finally
		{
			converter.Release.TrySetResult();
			host.Cancel();
			try { await first; } catch (OperationCanceledException) when (host.IsCancellationRequested) { }
		}
	}

	[Fact]
	public async Task HostCancellation_ReconstructedWorkerUsesPersistedSuccessorWithoutNewUploadAsync()
	{
		await using var fixture = new AvatarConcurrencyFixture();
		await AcceptImageAsync(fixture, null, ImageTestData.Raster(SkiaSharp.SKEncodedImageFormat.Png), "image/png");
		var validator = new SkiaAvatarImageValidator();
		var blocked = new GatedConverter(validator);
		var signal = new AvatarConcurrencySignal(fixture.ConnectionString);
		using var host = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		Task interrupted = Task.Run(() => new AvatarWorker(fixture.CreateStore(), signal, new SqlAvatarConversionLock(fixture), blocked, validator).DrainAsync(host.Token));
		try
		{
			await blocked.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
			host.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interrupted);
			Assert.Equal(0, blocked.Active);
			var recoverySignal = new AvatarConcurrencySignal(fixture.ConnectionString);
			var successor = Assert.Single(await recoverySignal.ReadPendingAsync());
			Assert.Equal(TimeSpan.FromSeconds(10), successor.Delay);
			await using (var interruptedState = fixture.CreateDbContext())
			{
				var job = await interruptedState.AvatarOptimizationJobs.SingleAsync();
				Assert.Equal(AvatarJobStatus.Retry, job.Status);
				Assert.Equal(1, job.Attempts);
			}
			fixture.Advance(successor.Delay);
			await new AvatarWorker(fixture.CreateStore(), recoverySignal, new SqlAvatarConversionLock(fixture), new SkiaAvatarImageConverter(validator), validator).DrainAsync();
			await recoverySignal.DeleteAsync(successor.Receipt);
			Assert.Empty(await recoverySignal.ReadPendingAsync());
			await AssertDrainedAsync(fixture, 1);
			await using var after = fixture.CreateDbContext();
			Assert.Equal(2, (await after.AvatarOptimizationJobs.SingleAsync()).Attempts);
			output.WriteLine("Host cancellation retained 10-second SQL-persisted successor; reconstructed worker completed without new upload; attempts=2; pendingWakeRecords=0.");
		}
		finally
		{
			host.Cancel();
			try { await interrupted; } catch (OperationCanceledException) when (host.IsCancellationRequested) { }
		}
	}

	[Fact]
	public async Task ExpiredClaim_WithPersistedSuccessorRecoversUnderNewLockAndFencesOldPublicationAsync()
	{
		await using var fixture = new AvatarConcurrencyFixture();
		await AcceptImageAsync(fixture, null, ImageTestData.Raster(SkiaSharp.SKEncodedImageFormat.Png), "image/png");
		var signal = new AvatarConcurrencySignal(fixture.ConnectionString);
		AvatarWakeReceipt successor = await signal.SendAsync(TimeSpan.FromSeconds(10));
		AvatarWork stale;
		await using (IAsyncDisposable? exclusion = await new SqlAvatarConversionLock(fixture).TryAcquireAsync())
		{
			Assert.NotNull(exclusion);
			stale = Assert.IsType<AvatarWork>(await fixture.CreateStore().ClaimAsync(Guid.NewGuid()));
		}
		// Abandoned claim is intentionally not released; this is state recovery, not a killed runtime.
		fixture.Advance(TimeSpan.FromSeconds(31));
		var validator = new SkiaAvatarImageValidator();
		var reconstructedSignal = new AvatarConcurrencySignal(fixture.ConnectionString);
		Assert.Single(await reconstructedSignal.ReadPendingAsync());
		await new AvatarWorker(fixture.CreateStore(), reconstructedSignal, new SqlAvatarConversionLock(fixture), new SkiaAvatarImageConverter(validator), validator).DrainAsync();
		Assert.False(await fixture.CreateStore().CompleteAsync(stale, [99]));
		await fixture.CreateStore().ReleaseAsync(stale);
		await reconstructedSignal.DeleteAsync(successor);
		Assert.Empty(await reconstructedSignal.ReadPendingAsync());
		await AssertDrainedAsync(fixture, 1);
		await using var after = fixture.CreateDbContext();
		var queue = await after.AvatarQueueStates.SingleAsync();
		Assert.True(queue.Fence > stale.Fence);
		Assert.Equal(2, (await after.AvatarOptimizationJobs.SingleAsync()).Attempts);
		output.WriteLine("Expired claim recovered after 31 clock seconds under a distinct lock object; newer fence rejected stale publication; attempts=2; pendingWakeRecords=0.");
	}

	private static async Task AcceptImageAsync(AvatarConcurrencyFixture fixture, Guid? agentId, byte[] bytes, string contentType)
	{
		EfAvatarStore store = fixture.CreateStore();
		var admission = await store.PrepareAsync(fixture.OwnerId, agentId);
		await store.AcceptAsync(admission.Id, bytes, contentType);
	}

	private static async Task<Guid?> AddTargetAsync(AvatarConcurrencyFixture fixture, bool isAgent)
	{
		if (!isAgent)
			return null;
		Guid agentId = Guid.NewGuid();
		await using var setup = fixture.CreateDbContext();
		setup.Agents.Add(new Agent { Id = agentId, OwnerUserId = fixture.OwnerId, Name = "Synthetic parallel target" });
		await setup.SaveChangesAsync();
		return agentId;
	}

	private static async Task<AdmissionResult> AdmitAsync(EfAvatarStore store, Guid admissionId, byte[] bytes)
	{
		try { return new AdmissionResult(await store.AcceptAsync(admissionId, bytes, "image/png"), null); }
		catch (PlanForgeException exception) { return new AdmissionResult(null, exception); }
	}

	private static async Task AssertDrainedAsync(AvatarConcurrencyFixture fixture, int expectedJobs)
	{
		Assert.False(await fixture.CreateStore().HasOutstandingAsync());
		await using var after = fixture.CreateDbContext();
		var jobs = await after.AvatarOptimizationJobs.ToListAsync();
		Assert.Equal(expectedJobs, jobs.Count);
		Assert.All(jobs, job =>
		{
			Assert.Equal(AvatarJobStatus.Completed, job.Status);
			Assert.False(job.IsCurrent);
			Assert.Null(job.RetainedBytes);
			Assert.Null(job.ReaderUntilUtc);
		});
		Assert.All(await after.Users.ToListAsync(), user =>
		{
			Assert.Equal("image/webp", user.AvatarContentType);
			Assert.False(user.AvatarTemporary);
			new SkiaAvatarImageValidator().Validate(user.AvatarBytes!, "image/webp");
		});
		Assert.All(await after.Agents.ToListAsync(), agent =>
		{
			Assert.Equal("image/webp", agent.AvatarContentType);
			Assert.False(agent.AvatarTemporary);
			new SkiaAvatarImageValidator().Validate(agent.AvatarBytes!, "image/webp");
		});
	}

	private static async Task<T[]> RaceAsync<T>(Func<Task<T>> first, Func<Task<T>> second)
	{
		using var barrier = new Barrier(3);
		Task<T> firstTask = Task.Run(async () =>
		{
			Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
			return await first();
		});
		Task<T> secondTask = Task.Run(async () =>
		{
			Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
			return await second();
		});
		Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
		return await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(20));
	}

	private static async Task<(byte[]? Bytes, string? ContentType, Guid Generation, bool Temporary)> ReadPhotoAsync(AvatarConcurrencyFixture fixture, Guid? agentId)
	{
		await using var database = fixture.CreateDbContext();
		if (agentId.HasValue)
		{
			var agent = await database.Agents.SingleAsync(item => item.Id == agentId);
			return (agent.AvatarBytes, agent.AvatarContentType, agent.AvatarGeneration, agent.AvatarTemporary);
		}
		var user = await database.Users.SingleAsync();
		return (user.AvatarBytes, user.AvatarContentType, user.AvatarGeneration, user.AvatarTemporary);
	}

	private sealed record AdmissionResult(AvatarState? State, PlanForgeException? Error);

	private sealed class GatedConverter(IAvatarImageValidator validator) : IAvatarConverter
	{
		private int active;
		private int calls;
		private int maximumActive;
		public int Active => Volatile.Read(ref active);
		public int Calls => Volatile.Read(ref calls);
		public System.Collections.Concurrent.ConcurrentQueue<byte[]> Inputs { get; } = new();
		public int MaximumActive => Volatile.Read(ref maximumActive);
		public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public async Task<byte[]> ConvertAsync(byte[] bytes, string contentType, CancellationToken cancellationToken = default)
		{
			Interlocked.Increment(ref calls);
			int readers = Interlocked.Increment(ref active);
			int previous;
			do { previous = Volatile.Read(ref maximumActive); }
			while (readers > previous && Interlocked.CompareExchange(ref maximumActive, readers, previous) != previous);
			Inputs.Enqueue(bytes);
			Started.TrySetResult();
			try
			{
				await Release.Task.WaitAsync(cancellationToken);
				return await new SkiaAvatarImageConverter(validator).ConvertAsync(bytes, contentType, cancellationToken);
			}
			finally { Interlocked.Decrement(ref active); }
		}
	}
}
