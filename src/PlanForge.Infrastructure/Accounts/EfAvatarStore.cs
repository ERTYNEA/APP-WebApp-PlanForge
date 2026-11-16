namespace PlanForge.Infrastructure.Accounts;

using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Common;
using PlanForge.Contracts.Accounts;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Database;
using System.Data;

/// <summary>Serializes avatar admission and publication independently of HTTP and broker instances.</summary>
public sealed class EfAvatarStore(IDbContextFactory<PlanForgeDbContext> factory, TimeProvider clock) : PlanForge.Application.Accounts.IAvatarStore
{
	public const long TemporaryOwnerLimit = 40L * 1024 * 1024;
	public async Task<bool> RenewAsync(PlanForge.Application.Accounts.AvatarWork work, CancellationToken cancellationToken = default)
	{
		await using PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await LockAsync(database, cancellationToken);
		var queue = await database.AvatarQueueStates.SingleAsync(cancellationToken);
		var job = await database.AvatarOptimizationJobs.SingleOrDefaultAsync(item => item.Id == work.JobId && item.Generation == work.Generation && item.IsCurrent && item.Status == AvatarJobStatus.Processing, cancellationToken);
		if (job is null || queue.LeaseOwner != work.WorkerId || queue.Fence != work.Fence || queue.LeaseUntilUtc <= clock.GetUtcNow().UtcDateTime)
			return false;
		queue.LeaseUntilUtc = clock.GetUtcNow().UtcDateTime.AddSeconds(30);
		job.ReaderUntilUtc = queue.LeaseUntilUtc;
		await database.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return true;
	}
	public async Task<bool> HasOutstandingAsync(CancellationToken cancellationToken = default)
	{
		await using PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await LockAsync(database, cancellationToken);
		DateTime now = clock.GetUtcNow().UtcDateTime;
		await database.AvatarAdmissions.Where(admission => admission.Accepted || admission.ExpiresAtUtc <= now).ExecuteDeleteAsync(cancellationToken);
		bool outstanding = await database.AvatarOptimizationJobs.AnyAsync(job => job.IsCurrent || job.RetainedBytes != null, cancellationToken)
			|| await database.AvatarAdmissions.AnyAsync(admission => !admission.Accepted && admission.ExpiresAtUtc > now, cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return outstanding;
	}
	public async Task<bool> HasRetainedAsync(Guid ownerId, Guid? agentId, CancellationToken cancellationToken = default)
	{
		await using PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		return await database.AvatarOptimizationJobs.AnyAsync(job => job.OwnerUserId == ownerId && job.AgentId == agentId && job.RetainedBytes != null, cancellationToken);
	}

	public async Task<AvatarState> AcceptAsync(Guid admissionId, byte[] bytes, string contentType, CancellationToken cancellationToken = default)
	{
		await using PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await LockAsync(database, cancellationToken);
		AvatarAdmission admission = await database.AvatarAdmissions.SingleOrDefaultAsync(item => item.Id == admissionId, cancellationToken)
			?? throw new PlanForgeException(409, "Avatar acceptance expired. Please upload again.");
		if (admission.Accepted || admission.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
			throw new PlanForgeException(409, "Avatar acceptance expired. Please upload again.");
		await RequireOwnerAsync(database, admission.OwnerUserId, admission.AgentId, cancellationToken);
		AvatarOptimizationJob? job = await database.AvatarOptimizationJobs.SingleOrDefaultAsync(item => item.OwnerUserId == admission.OwnerUserId && item.TargetId == (admission.AgentId ?? admission.OwnerUserId) && item.IsAgent == admission.AgentId.HasValue && item.IsCurrent, cancellationToken);
		var previous = job?.Status == AvatarJobStatus.Processing ? await CurrentAsync(database, admission.OwnerUserId, admission.AgentId, cancellationToken) : null;
		long retained = await database.AvatarOptimizationJobs.Where(job => job.OwnerUserId == admission.OwnerUserId && job.RetainedBytes != null).SumAsync(job => (long)job.RetainedBytes!.Length, cancellationToken);
		long users = await database.Users.Where(user => user.Id == admission.OwnerUserId && user.AvatarTemporary && user.AvatarBytes != null && admission.AgentId != null).SumAsync(user => (long)user.AvatarBytes!.Length, cancellationToken);
		long agents = await database.Agents.Where(agent => agent.OwnerUserId == admission.OwnerUserId && agent.AvatarTemporary && agent.AvatarBytes != null && agent.Id != admission.AgentId).SumAsync(agent => (long)agent.AvatarBytes!.Length, cancellationToken);
		if (retained + users + agents + bytes.LongLength + (previous?.Bytes.LongLength ?? 0) > TemporaryOwnerLimit)
			throw new PlanForgeException(413, "Unable to upload avatar right now. Please try again in a few minutes.");
		var queue = await database.AvatarQueueStates.SingleAsync(cancellationToken);
		Guid generation = Guid.NewGuid();
		await SetCurrentAsync(database, admission.OwnerUserId, admission.AgentId, bytes, contentType, generation, true, cancellationToken);
		if (job?.Status == AvatarJobStatus.Processing)
		{
			job.IsCurrent = false;
			job.Status = AvatarJobStatus.Cancelling;
			job.RetainedBytes = previous?.Bytes;
			await database.SaveChangesAsync(cancellationToken);
			job = null;
		}
		if (job is null)
		{
			job = new AvatarOptimizationJob
			{
				OwnerUserId = admission.OwnerUserId,
				AgentId = admission.AgentId,
				TargetId = admission.AgentId ?? admission.OwnerUserId,
				IsAgent = admission.AgentId.HasValue,
				Sequence = ++queue.NextSequence
			};
			database.AvatarOptimizationJobs.Add(job);
		}
		job.Generation = generation;
		job.Status = AvatarJobStatus.Pending;
		job.Attempts = 0;
		job.AvailableAtUtc = clock.GetUtcNow().UtcDateTime;
		admission.Accepted = true;
		await database.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return new AvatarState(true, generation);
	}

	public async Task<PlanForge.Application.Accounts.AvatarWork?> ClaimAsync(Guid workerId, CancellationToken cancellationToken = default)
	{
		await using PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await LockAsync(database, cancellationToken);
		var queue = await database.AvatarQueueStates.SingleAsync(cancellationToken);
		DateTime now = clock.GetUtcNow().UtcDateTime;
		if (queue.LeaseOwner.HasValue && queue.LeaseUntilUtc > now)
			return null;
		// Caller holds the SQL session conversion lock: an expired reader cannot overlap this recovery.
		foreach (var abandoned in await database.AvatarOptimizationJobs.Where(item => item.Status == AvatarJobStatus.Processing || item.Status == AvatarJobStatus.Cancelling).ToListAsync(cancellationToken))
		{
			abandoned.RetainedBytes = null;
			abandoned.ReaderUntilUtc = null;
			if (!abandoned.IsCurrent)
				abandoned.Status = AvatarJobStatus.Cancelled;
			else if (abandoned.Attempts < 3)
			{
				abandoned.Status = AvatarJobStatus.Retry;
				abandoned.Sequence = ++queue.NextSequence;
			}
			else
			{
				if ((await CurrentAsync(database, abandoned.OwnerUserId, abandoned.AgentId, cancellationToken))?.Version == abandoned.Generation)
					await SetCurrentAsync(database, abandoned.OwnerUserId, abandoned.AgentId, null, null, Guid.NewGuid(), false, cancellationToken);
				abandoned.Status = AvatarJobStatus.Failed;
				abandoned.IsCurrent = false;
			}
		}
		queue.LeaseOwner = null;
		queue.LeaseUntilUtc = null;
		await database.SaveChangesAsync(cancellationToken);
		AvatarOptimizationJob? job = await database.AvatarOptimizationJobs.Where(item => item.IsCurrent && (item.Status == AvatarJobStatus.Pending || item.Status == AvatarJobStatus.Retry) && item.AvailableAtUtc <= now)
			.OrderBy(item => item.Sequence).FirstOrDefaultAsync(cancellationToken);
		if (job is null)
		{
			await transaction.CommitAsync(cancellationToken);
			return null;
		}
		var content = await CurrentAsync(database, job.OwnerUserId, job.AgentId, cancellationToken);
		if (content is null || content.Version != job.Generation)
			throw new InvalidOperationException("Avatar generation is inconsistent.");
		queue.LeaseOwner = workerId;
		queue.LeaseUntilUtc = now.AddSeconds(30);
		queue.Fence++;
		job.WorkerId = workerId;
		job.Fence = queue.Fence;
		job.ReaderUntilUtc = queue.LeaseUntilUtc;
		job.Status = AvatarJobStatus.Processing;
		job.Attempts++;
		await database.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return new PlanForge.Application.Accounts.AvatarWork(job.Id, job.Generation, workerId, job.Fence, content.Bytes, content.ContentType);
	}

	public async Task<bool> CompleteAsync(PlanForge.Application.Accounts.AvatarWork work, byte[] optimized, CancellationToken cancellationToken = default)
	{
		await using PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await LockAsync(database, cancellationToken);
		var queue = await database.AvatarQueueStates.SingleAsync(cancellationToken);
		var job = await database.AvatarOptimizationJobs.SingleOrDefaultAsync(item => item.Id == work.JobId, cancellationToken);
		if (job is null || !job.IsCurrent || job.Generation != work.Generation || job.Status != AvatarJobStatus.Processing
			|| queue.LeaseOwner != work.WorkerId || queue.Fence != work.Fence || queue.LeaseUntilUtc <= clock.GetUtcNow().UtcDateTime)
			return false;
		var content = await CurrentAsync(database, job.OwnerUserId, job.AgentId, cancellationToken);
		if (content?.Version != work.Generation)
			return false;
		// Replacing the only authoritative BLOB and retiring its job is one atomic commit: no gap or orphan.
		await SetCurrentAsync(database, job.OwnerUserId, job.AgentId, optimized, "image/webp", Guid.NewGuid(), false, cancellationToken);
		job.Status = AvatarJobStatus.Completed;
		job.IsCurrent = false;
		job.RetainedBytes = null;
		job.ReaderUntilUtc = null;
		queue.LeaseOwner = null;
		queue.LeaseUntilUtc = null;
		await database.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	private static Task<PlanForge.Application.Accounts.AvatarContent?> CurrentAsync(PlanForgeDbContext database, Guid ownerId, Guid? agentId, CancellationToken cancellationToken)
		=> agentId.HasValue
			? database.Agents.Where(agent => agent.Id == agentId && agent.OwnerUserId == ownerId && agent.AvatarBytes != null).Select(agent => new PlanForge.Application.Accounts.AvatarContent(agent.AvatarBytes!, agent.AvatarContentType!, agent.AvatarGeneration)).SingleOrDefaultAsync(cancellationToken)
			: database.Users.Where(user => user.Id == ownerId && user.AvatarBytes != null).Select(user => new PlanForge.Application.Accounts.AvatarContent(user.AvatarBytes!, user.AvatarContentType!, user.AvatarGeneration)).SingleOrDefaultAsync(cancellationToken);

	public async Task FailAsync(PlanForge.Application.Accounts.AvatarWork work, CancellationToken cancellationToken = default)
	{
		await using PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await LockAsync(database, cancellationToken);
		var queue = await database.AvatarQueueStates.SingleAsync(cancellationToken);
		var job = await database.AvatarOptimizationJobs.SingleOrDefaultAsync(item => item.Id == work.JobId, cancellationToken);
		if (job is null || job.Generation != work.Generation || queue.LeaseOwner != work.WorkerId || queue.Fence != work.Fence)
			return;
		if (job.Status == AvatarJobStatus.Processing)
		{
			if (job.IsCurrent && job.Attempts < 3)
			{
				job.Status = AvatarJobStatus.Retry;
				job.Sequence = ++queue.NextSequence;
				job.AvailableAtUtc = clock.GetUtcNow().UtcDateTime.AddSeconds(job.Attempts * 5);
			}
			else
			{
				if (job.IsCurrent && (await CurrentAsync(database, job.OwnerUserId, job.AgentId, cancellationToken))?.Version == work.Generation)
					await SetCurrentAsync(database, job.OwnerUserId, job.AgentId, null, null, Guid.NewGuid(), false, cancellationToken);
				job.Status = job.IsCurrent ? AvatarJobStatus.Failed : AvatarJobStatus.Cancelled;
				job.IsCurrent = false;
			}
		}
		else if (job.Status == AvatarJobStatus.Cancelling)
			job.Status = AvatarJobStatus.Cancelled;
		job.RetainedBytes = null;
		job.ReaderUntilUtc = null;
		queue.LeaseOwner = null;
		queue.LeaseUntilUtc = null;
		await database.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
	}

	/// <summary>Called only after conversion has stopped using its immutable byte snapshot.</summary>
	public Task ReleaseAsync(PlanForge.Application.Accounts.AvatarWork work, CancellationToken cancellationToken = default) => FailAsync(work, cancellationToken);

	/// <summary>Invalidates publication immediately. The caller separately awaits retained-reader cleanup before confirming deletion.</summary>
	public async Task<AvatarState> DeleteAsync(Guid ownerId, Guid? agentId, CancellationToken cancellationToken = default, Guid? admissionId = null)
	{
		await using PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await LockAsync(database, cancellationToken);
		await RequireOwnerAsync(database, ownerId, agentId, cancellationToken);
		var job = await database.AvatarOptimizationJobs.SingleOrDefaultAsync(item => item.OwnerUserId == ownerId && item.TargetId == (agentId ?? ownerId) && item.IsAgent == agentId.HasValue && item.IsCurrent, cancellationToken);
		if (admissionId.HasValue)
		{
			var admission = await database.AvatarAdmissions.SingleOrDefaultAsync(item => item.Id == admissionId && item.OwnerUserId == ownerId && item.AgentId == agentId, cancellationToken);
			if (admission is null || admission.Accepted || admission.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
				throw new PlanForgeException(409, "Avatar acceptance expired. Please upload again.");
			admission.Accepted = true;
		}
		if (job is not null)
		{
			bool reading = job.Status == AvatarJobStatus.Processing;
			job.Status = reading ? AvatarJobStatus.Cancelling : AvatarJobStatus.Cancelled;
			job.RetainedBytes = reading ? (await CurrentAsync(database, ownerId, agentId, cancellationToken))?.Bytes : null;
			job.IsCurrent = false;
		}
		Guid generation = Guid.NewGuid();
		await SetCurrentAsync(database, ownerId, agentId, null, null, generation, false, cancellationToken);
		await database.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return new AvatarState(false, generation);
	}

	public async Task<AvatarAdmission> PrepareAsync(Guid ownerId, Guid? agentId, CancellationToken cancellationToken = default)
	{
		await using PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await LockAsync(database, cancellationToken);
		await RequireOwnerAsync(database, ownerId, agentId, cancellationToken);
		var admission = new AvatarAdmission { OwnerUserId = ownerId, AgentId = agentId, ExpiresAtUtc = clock.GetUtcNow().UtcDateTime.AddSeconds(30) };
		database.AvatarAdmissions.Add(admission);
		await database.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return admission;
	}

	private static async Task LockAsync(PlanForgeDbContext database, CancellationToken cancellationToken)
	{
		// The UPDATE takes a database write lock before any eligibility/quota reads, across API instances.
		if (await database.AvatarQueueStates.Where(state => state.Id == 1).ExecuteUpdateAsync(setters => setters.SetProperty(state => state.NextSequence, state => state.NextSequence), cancellationToken) != 1)
			throw new InvalidOperationException("Avatar queue schema has not been initialized.");
	}

	private static async Task RequireOwnerAsync(PlanForgeDbContext database, Guid ownerId, Guid? agentId, CancellationToken cancellationToken)
	{
		if (!await database.Users.AnyAsync(user => user.Id == ownerId && user.IsActive, cancellationToken)
			|| agentId.HasValue && !await database.Agents.AnyAsync(agent => agent.Id == agentId && agent.OwnerUserId == ownerId, cancellationToken))
			throw new PlanForgeException(404, "Avatar owner not found.");
	}

	private static async Task SetCurrentAsync(PlanForgeDbContext database, Guid ownerId, Guid? agentId, byte[]? bytes, string? contentType, Guid generation, bool temporary, CancellationToken cancellationToken)
	{
		if (agentId.HasValue)
			await database.Agents.Where(agent => agent.Id == agentId && agent.OwnerUserId == ownerId).ExecuteUpdateAsync(setters => setters
				.SetProperty(agent => agent.AvatarBytes, bytes).SetProperty(agent => agent.AvatarContentType, contentType)
				.SetProperty(agent => agent.AvatarGeneration, generation).SetProperty(agent => agent.AvatarTemporary, temporary), cancellationToken);
		else
			await database.Users.Where(user => user.Id == ownerId).ExecuteUpdateAsync(setters => setters
				.SetProperty(user => user.AvatarBytes, bytes).SetProperty(user => user.AvatarContentType, contentType)
				.SetProperty(user => user.AvatarGeneration, generation).SetProperty(user => user.AvatarTemporary, temporary), cancellationToken);
	}
}
