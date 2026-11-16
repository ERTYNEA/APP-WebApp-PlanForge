namespace PlanForge.Infrastructure.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PlanForge.Application.Accounts;
using PlanForge.Domain.Accounts;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Database;
using System.Data.Common;

/// <summary>Private-cache, file-backed relational contexts; never shares an EF context across contenders.</summary>
internal sealed class AvatarConcurrencyFixture : IDbContextFactory<PlanForgeDbContext>, IAsyncDisposable
{
	private readonly string directory;
	private readonly DbContextOptions<PlanForgeDbContext> options;
	private readonly TransactionRaceGate transactionRace = new();
	private long clockTicks = DateTimeOffset.UtcNow.UtcTicks;
	private int contextCount;

	public AvatarConcurrencyFixture()
	{
		string root = Environment.GetEnvironmentVariable("PLANFORGE_AVATAR_TEST_ROOT")
			?? Path.Combine(AppContext.BaseDirectory, "avatar-concurrency-data");
		directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		ConnectionString = new SqliteConnectionStringBuilder
		{
			DataSource = Path.Combine(directory, "avatars.db"),
			Cache = SqliteCacheMode.Private,
			Pooling = false,
			DefaultTimeout = 5
		}.ToString();
		options = new DbContextOptionsBuilder<PlanForgeDbContext>().UseSqlite(ConnectionString).AddInterceptors(transactionRace).Options;
		using var database = CreateDbContext();
		database.Database.EnsureCreated();
		database.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
		database.Users.Add(new UserAccount
		{
			Id = OwnerId,
			Email = "parallel-avatar@example.test",
			NormalizedEmail = "PARALLEL-AVATAR@EXAMPLE.TEST",
			DisplayName = "Synthetic concurrency fixture",
			PasswordHash = "not-a-credential"
		});
		database.SaveChanges();
		// Audit rows participate in the same write transaction as each official-generation update.
		database.Database.ExecuteSqlRaw("CREATE TABLE AvatarCommitAudit (Id INTEGER PRIMARY KEY AUTOINCREMENT, TargetId TEXT NOT NULL, Generation TEXT NOT NULL);");
		database.Database.ExecuteSqlRaw("CREATE TRIGGER avatar_user_commit AFTER UPDATE OF AvatarGeneration ON Users BEGIN INSERT INTO AvatarCommitAudit(TargetId, Generation) VALUES (NEW.Id, NEW.AvatarGeneration); END;");
		database.Database.ExecuteSqlRaw("CREATE TRIGGER avatar_agent_commit AFTER UPDATE OF AvatarGeneration ON Agents BEGIN INSERT INTO AvatarCommitAudit(TargetId, Generation) VALUES (NEW.Id, NEW.AvatarGeneration); END;");
		database.Database.ExecuteSqlRaw("CREATE TABLE AvatarTestSignals (Id TEXT PRIMARY KEY, DelayTicks INTEGER NOT NULL, Deleted INTEGER NOT NULL DEFAULT 0);");
		Clock = new FixtureClock(this);
	}

	public TimeProvider Clock { get; }
	public string ConnectionString { get; }
	public int ContextCount => Volatile.Read(ref contextCount);
	public bool TransactionOverlapVerified => transactionRace.OverlapVerified;
	public Guid OwnerId { get; } = Guid.NewGuid();

	public void Advance(TimeSpan interval) => Interlocked.Add(ref clockTicks, interval.Ticks);
	public void ArmTransactionRace() => transactionRace.Arm();
	public EfAvatarStore CreateStore() => new(this, Clock);

	public PlanForgeDbContext CreateDbContext()
	{
		Interlocked.Increment(ref contextCount);
		return new PlanForgeDbContext(options);
	}

	public ValueTask DisposeAsync()
	{
		Directory.Delete(directory, true);
		return ValueTask.CompletedTask;
	}

	public async Task<Guid[]> ReadCommitsAsync(Guid targetId)
	{
		await using var connection = new SqliteConnection(ConnectionString);
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT Generation FROM AvatarCommitAudit WHERE TargetId = $target ORDER BY Id;";
		command.Parameters.AddWithValue("$target", targetId.ToString().ToUpperInvariant());
		var result = new List<Guid>();
		await using var reader = await command.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			result.Add(Guid.Parse(reader.GetString(0)));
		return result.ToArray();
	}

	private sealed class FixtureClock(AvatarConcurrencyFixture fixture) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref fixture.clockTicks), TimeSpan.Zero);
	}

	/// <summary>Holds the first writer before commit until the other independent context attempts BEGIN.</summary>
	private sealed class TransactionRaceGate : DbTransactionInterceptor
	{
		private TaskCompletionSource? secondAttempt;
		private int attempts;
		private int commits;
		public bool OverlapVerified { get; private set; }

		public void Arm() => secondAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
		{
			if (secondAttempt is not null && Interlocked.Increment(ref attempts) == 2)
				secondAttempt.TrySetResult();
			return ValueTask.FromResult(result);
		}

		public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
		{
			if (secondAttempt is not null && Interlocked.Increment(ref commits) == 1)
			{
				await secondAttempt.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
				OverlapVerified = true;
			}
			return result;
		}
	}
}

/// <summary>Persisted test wake ledger, not an Azure Queue or a Functions-host emulator.</summary>
internal sealed class AvatarConcurrencySignal(string connectionString) : IAvatarWakeSignal
{
	public async Task<AvatarWakeReceipt> SendAsync(TimeSpan delay, CancellationToken cancellationToken = default)
	{
		var receipt = new AvatarWakeReceipt(Guid.NewGuid().ToString(), "synthetic-pop-receipt");
		await using var connection = new SqliteConnection(connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "INSERT INTO AvatarTestSignals(Id, DelayTicks) VALUES ($id, $ticks);";
		command.Parameters.AddWithValue("$id", receipt.MessageId);
		command.Parameters.AddWithValue("$ticks", delay.Ticks);
		await command.ExecuteNonQueryAsync(cancellationToken);
		return receipt;
	}

	public async Task DeleteAsync(AvatarWakeReceipt receipt, CancellationToken cancellationToken = default)
	{
		await using var connection = new SqliteConnection(connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "UPDATE AvatarTestSignals SET Deleted = 1 WHERE Id = $id;";
		command.Parameters.AddWithValue("$id", receipt.MessageId);
		Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
	}

	public async Task<(AvatarWakeReceipt Receipt, TimeSpan Delay)[]> ReadPendingAsync()
	{
		await using var connection = new SqliteConnection(connectionString);
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT Id, DelayTicks FROM AvatarTestSignals WHERE Deleted = 0 ORDER BY rowid;";
		var result = new List<(AvatarWakeReceipt, TimeSpan)>();
		await using var reader = await command.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			result.Add((new AvatarWakeReceipt(reader.GetString(0), "synthetic-pop-receipt"), TimeSpan.FromTicks(reader.GetInt64(1))));
		return result.ToArray();
	}
}
