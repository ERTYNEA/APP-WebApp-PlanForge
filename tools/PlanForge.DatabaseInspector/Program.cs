using Microsoft.Data.SqlClient;
using PlanForge.Infrastructure.Database;

string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PlanForgeDb");
if (string.IsNullOrWhiteSpace(connectionString))
	throw new InvalidOperationException("The deployment SQL connection environment variable is required.");
SqlConnectionSettings.ValidateDeployment(connectionString);
try
{
	await using var connection = new SqlConnection(connectionString);
	await connection.OpenAsync();
	await using var permissions = new SqlCommand("SELECT IS_ROLEMEMBER('db_owner'), HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE'), HAS_PERMS_BY_NAME('dbo', 'SCHEMA', 'ALTER')", connection);
	await using (SqlDataReader reader = await permissions.ExecuteReaderAsync())
	{
		await reader.ReadAsync();
		Console.WriteLine($"Migrator permissions: db_owner={reader.GetValue(0)}, CREATE TABLE={reader.GetValue(1)}, ALTER dbo={reader.GetValue(2)}");
		if (Convert.ToInt32(reader.GetValue(1)) != 1 || Convert.ToInt32(reader.GetValue(2)) != 1)
			throw new InvalidOperationException("Migrator lacks required schema permissions; no migration was attempted.");
	}
	await using var tables = new SqlCommand("SELECT name FROM sys.tables ORDER BY name", connection);
	await using (SqlDataReader reader = await tables.ExecuteReaderAsync())
		while (await reader.ReadAsync())
			Console.WriteLine($"Existing table: {reader.GetString(0)}");
	await using var size = new SqlCommand("SELECT SUM(size)*8.0/1024 FROM sys.database_files WHERE type=0", connection);
	Console.WriteLine($"Allocated SQL data MB: {await size.ExecuteScalarAsync()}");
}
catch (SqlException exception)
{
	Console.Error.WriteLine($"SQL inspection failed: number={exception.Number}, state={exception.State}; connection and credential values suppressed.");
	Environment.ExitCode = 1;
}
