namespace PlanForge.Infrastructure.Accounts;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PlanForge.Application.Accounts;
using PlanForge.Infrastructure.Database;
using System.Collections.Concurrent;

/// <summary>SQL session applock survives short admission transactions. SQLite exclusion is for isolated single-process QA only.</summary>
public sealed class SqlAvatarConversionLock(IDbContextFactory<PlanForgeDbContext> factory) : IAvatarConversionLock
{
	private static readonly ConcurrentDictionary<string, SemaphoreSlim> sqliteGates = new(StringComparer.Ordinal);

	public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken = default)
	{
		PlanForgeDbContext database = await factory.CreateDbContextAsync(cancellationToken);
		if (database.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
		{
			string key = database.Database.GetConnectionString()!;
			await database.DisposeAsync();
			SemaphoreSlim gate = sqliteGates.GetOrAdd(key, _ => new SemaphoreSlim(1));
			return await gate.WaitAsync(0, cancellationToken) ? new SqliteHolder(gate) : null;
		}
		if (!database.Database.IsSqlServer())
		{
			await database.DisposeAsync();
			throw new NotSupportedException("Avatar conversion requires SQL Server session exclusion.");
		}
		try
		{
			await database.Database.OpenConnectionAsync(cancellationToken);
			await using var command = database.Database.GetDbConnection().CreateCommand();
			command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = N'PlanForge.Avatar.Converter', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0; SELECT @result;";
			command.CommandTimeout = 5;
			int result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
			if (result >= 0)
				return new SqlHolder(database);
			await database.DisposeAsync();
			return null;
		}
		catch
		{
			if (database.Database.GetDbConnection() is SqlConnection connection)
				SqlConnection.ClearPool(connection);
			await database.DisposeAsync();
			throw;
		}
	}

	private sealed class SqlHolder : IAvatarConversionLease
	{
		private readonly MonitoredAvatarConversionLease monitor;

		public SqlHolder(PlanForgeDbContext database)
			=> monitor = new(token => CheckAsync(database, token), () => ReleaseAsync(database), TimeSpan.FromMilliseconds(500));

		public CancellationToken LostToken => monitor.LostToken;
		public ValueTask DisposeAsync() => monitor.DisposeAsync();

		private static async Task<bool> CheckAsync(PlanForgeDbContext database, CancellationToken cancellationToken)
		{
			if (database.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
				return false;
			await using var command = database.Database.GetDbConnection().CreateCommand();
			command.CommandText = "SELECT APPLOCK_MODE(N'public', N'PlanForge.Avatar.Converter', N'Session');";
			command.CommandTimeout = 2;
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(TimeSpan.FromSeconds(2));
			return string.Equals(await command.ExecuteScalarAsync(timeout.Token) as string, "Exclusive", StringComparison.Ordinal);
		}

		private static async ValueTask ReleaseAsync(PlanForgeDbContext database)
		{
			try
			{
				if (database.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
					return;
				await using var command = database.Database.GetDbConnection().CreateCommand();
				command.CommandText = "IF APPLOCK_MODE(N'public', N'PlanForge.Avatar.Converter', N'Session') = N'Exclusive' BEGIN DECLARE @result int; EXEC @result = sys.sp_releaseapplock @Resource = N'PlanForge.Avatar.Converter', @LockOwner = N'Session'; SELECT @result; END ELSE SELECT 0;";
				command.CommandTimeout = 5;
				if (Convert.ToInt32(await command.ExecuteScalarAsync()) < 0)
					throw new InvalidOperationException("Avatar conversion exclusion could not be released.");
			}
			catch
			{
				SqlConnection.ClearPool((SqlConnection)database.Database.GetDbConnection());
				throw;
			}
			finally
			{
				await database.DisposeAsync();
			}
		}
	}

	private sealed class SqliteHolder(SemaphoreSlim gate) : IAsyncDisposable
	{
		private int disposed;
		public ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref disposed, 1) == 0)
				gate.Release();
			return ValueTask.CompletedTask;
		}
	}
}
