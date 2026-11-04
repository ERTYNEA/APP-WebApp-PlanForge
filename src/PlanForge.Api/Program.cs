using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlanForge.Api.Http;
using PlanForge.Api.Library;
using PlanForge.Application.Accounts;
using PlanForge.Application.Agents;
using PlanForge.Application.Exchange;
using PlanForge.Application.Library;
using PlanForge.Application.Planning;
using PlanForge.Infrastructure.Accounts;
using PlanForge.Infrastructure.Agents;
using PlanForge.Infrastructure.Database;
using PlanForge.Infrastructure.Planning;
using PlanForge.Infrastructure.Library;

var host = new HostBuilder()
	.ConfigureFunctionsWorkerDefaults()
	.ConfigureServices((context, services) =>
	{
		string connectionString = context.Configuration.GetConnectionString("PlanForgeDb")
			?? throw new InvalidOperationException("PlanForge SQL configuration is required.");
		services.AddDbContext<PlanForgeDbContext>(options => options.UseSqlServer(connectionString));
		services.AddScoped<IAccountStore, EfAccountStore>();
		services.AddSingleton<ISecretProtector, SecretProtector>();
		services.AddSingleton(TimeProvider.System);
		services.AddScoped<AccountService>();
		services.AddScoped<AccountManagementService>();
		services.AddScoped<IProjectStore, EfProjectStore>();
		services.AddScoped<ProjectService>();
		services.AddScoped<PlanEditor>();
		services.AddScoped<IAgentStore, EfAgentStore>();
		services.AddScoped<AgentService>();
		services.AddScoped<WorkPlanExchange>();
		services.AddSingleton(ResourceLimitConfiguration.Read(context.Configuration));
		services.AddScoped<ILibraryStore, EfLibraryStore>();
		services.AddScoped<LibraryService>();
		services.AddScoped<ProjectArchiveExporter>();
		services.AddSingleton<HttpEndpointRunner>();
	})
	.Build();

await host.RunAsync();
