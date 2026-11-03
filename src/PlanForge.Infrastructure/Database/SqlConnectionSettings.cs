namespace PlanForge.Infrastructure.Database;

public static class SqlConnectionSettings
{
	public static void ValidateDeployment(string connectionString)
	{
		var settings = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
		if (settings.Encrypt == Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Optional || settings.TrustServerCertificate || settings.UserID != "planforge_migrator" || settings.InitialCatalog != "PlanForge-DB")
			throw new InvalidOperationException("Deployment requires certificate-validated encrypted SQL authentication as planforge_migrator against PlanForge-DB.");
	}

	public static string FromCredentialLines(IEnumerable<string> lines)
	{
		Dictionary<string, string> values = lines.Where(line => line.StartsWith("SQL_", StringComparison.Ordinal))
			.Select(line => line.Split('=', 2)).ToDictionary(parts => parts[0], parts => parts[1]);
		var settings = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
		{
			DataSource = "tcp:planforge-sql.database.windows.net,1433",
			InitialCatalog = "PlanForge-DB",
			UserID = values["SQL_USERNAME"],
			Password = values["SQL_PASSWORD"],
			Encrypt = Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Mandatory,
			TrustServerCertificate = false,
			ConnectTimeout = 20
		};
		return settings.ConnectionString;
	}
}
