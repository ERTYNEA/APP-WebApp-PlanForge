using Azure.Storage.Queues;
using Azure.Storage.Queues.Specialized;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlanForge.Api.Http;
using PlanForge.Api.Avatars;
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

if (args is ["--avatar-convert", string avatarContentType])
	return await PlanForge.Api.Avatars.IsolatedAvatarConverter.RunChildAsync(avatarContentType);

var host = new HostBuilder()
	.ConfigureFunctionsWorkerDefaults()
	.ConfigureServices((context, services) =>
	{
		string connectionString = context.Configuration.GetConnectionString("PlanForgeDb")
			?? throw new InvalidOperationException("PlanForge SQL configuration is required.");
		services.AddDbContextFactory<PlanForgeDbContext>(options => options.UseSqlServer(connectionString));
		services.AddSingleton(_ => AvatarQueueClientFactory.Create(context.Configuration));
		services.AddSingleton<IAvatarWakeSignal, QueueAvatarWakeSignal>();
		services.AddSingleton(provider =>
		{
			QueueClient queue = provider.GetRequiredService<QueueClient>();
			QueueClient recovery = queue.GetParentQueueServiceClient().GetQueueClient(QueueAvatarWakeSignal.QueueName + "-poison");
			return new AvatarQueueReadiness(queue, recovery, provider.GetRequiredService<ILogger<AvatarQueueReadiness>>());
		});
		services.AddSingleton<IAvatarQueueReadiness>(provider => provider.GetRequiredService<AvatarQueueReadiness>());
		services.AddHostedService(provider => provider.GetRequiredService<AvatarQueueReadiness>());
		services.AddSingleton<IAvatarImageValidator, SkiaAvatarImageValidator>();
		services.AddSingleton<IAvatarConverter>(_ => new IsolatedAvatarConverter());
		services.AddSingleton<IAvatarConversionLock, SqlAvatarConversionLock>();
		services.AddScoped<EfAvatarStore>();
		services.AddScoped<IAvatarStore>(provider => provider.GetRequiredService<EfAvatarStore>());
		services.AddScoped<AvatarService>();
		services.AddScoped<AvatarWorker>();
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
return 0;
