using AccountingLeads.Application.Common.Interfaces;
using AccountingLeads.Application.Common.Messaging;
using AccountingLeads.Application.Leads;
using AccountingLeads.Infrastructure.Ai;
using AccountingLeads.Infrastructure.Messaging;
using AccountingLeads.Infrastructure.Persistence;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingLeads.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }

    /// <summary>
    /// Registers the Azure Service Bus outbox publisher. Deliberately separate from
    /// <see cref="AddInfrastructure"/> and called only by the Worker — the Api never publishes
    /// to Service Bus, and requiring 'ServiceBus:*' configuration there would break Api startup
    /// (ASP.NET Core's dev-time ValidateOnBuild validates every registered service's dependency
    /// graph regardless of whether anything ever resolves it).
    /// </summary>
    public static IServiceCollection AddOutboxPublishing(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var serviceBusNamespace = configuration["ServiceBus:FullyQualifiedNamespace"]
            ?? throw new InvalidOperationException("Configuration value 'ServiceBus:FullyQualifiedNamespace' was not found.");
        var serviceBusQueueName = configuration["ServiceBus:QueueName"]
            ?? throw new InvalidOperationException("Configuration value 'ServiceBus:QueueName' was not found.");

        var clientOptions = new ServiceBusClientOptions
        {
            RetryOptions = new ServiceBusRetryOptions
            {
                Mode = ServiceBusRetryMode.Exponential,
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(0.8),
                MaxDelay = TimeSpan.FromSeconds(5),
                TryTimeout = TimeSpan.FromSeconds(10)
            }
        };

        // Managed Identity is only meaningful once the Worker runs on real Azure hosting
        // (Azure Container Apps — see docs/architecture.md §20). On a developer machine there's
        // no IMDS endpoint to answer it, and ManagedIdentityCredential's failure there surfaces as
        // AuthenticationFailedException rather than a cleanly-classified "unavailable" one —
        // which makes DefaultAzureCredential abort its whole fallback chain instead of moving
        // on to AzureCliCredential. Excluding it in Development lets `az login` work as intended.
        //
        // TenantId is explicitly pinned (via optional config, null in production where Managed
        // Identity ignores it anyway) because the developer's account is a personal/guest
        // identity in this subscription's tenant: on a machine with another signed-in credential
        // source (e.g. Visual Studio/VS Code), DefaultAzureCredential can resolve to a *different*
        // credential in its fallback chain that mints a token for that account's home tenant
        // instead of this subscription's tenant — a token that looks valid but is rejected by the
        // target resource. See docs/architecture.md §21 for the diagnosis (found via the Azure
        // OpenAI call failing with a generic 401 whose JWT `tid` claim didn't match the
        // subscription's tenant).
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeManagedIdentityCredential = isDevelopment,
            TenantId = configuration["Azure:TenantId"]
        });

        services.AddSingleton(_ => new ServiceBusClient(serviceBusNamespace, credential, clientOptions));
        services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(serviceBusQueueName));
        services.AddSingleton<IOutboxPublisher, ServiceBusOutboxPublisher>();

        return services;
    }

    /// <summary>
    /// Registers the Azure OpenAI-backed <see cref="ILeadAnalysisService"/> and the
    /// <see cref="ServiceBusProcessor"/> used to consume <c>lead-created</c> messages.
    /// Deliberately separate from <see cref="AddInfrastructure"/> and called only by the
    /// Worker, for the same ValidateOnBuild-isolation reason as <see cref="AddOutboxPublishing"/>
    /// — the Api never analyzes or consumes leads. Reuses the <see cref="ServiceBusClient"/>
    /// singleton already registered by <see cref="AddOutboxPublishing"/>, so that method must be
    /// called first.
    /// </summary>
    public static IServiceCollection AddLeadAnalysis(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Configuration value 'AzureOpenAI:Endpoint' was not found.");
        var deploymentName = configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("Configuration value 'AzureOpenAI:DeploymentName' was not found.");
        var serviceBusQueueName = configuration["ServiceBus:QueueName"]
            ?? throw new InvalidOperationException("Configuration value 'ServiceBus:QueueName' was not found.");

        // Same local-dev credential gotchas as AddOutboxPublishing: ManagedIdentityCredential
        // must be excluded off-Azure so DefaultAzureCredential can fall through to
        // AzureCliCredential, and TenantId must be pinned so a guest developer account doesn't
        // resolve to a different signed-in credential's home tenant. See the comment there.
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeManagedIdentityCredential = isDevelopment,
            TenantId = configuration["Azure:TenantId"]
        });

        services.AddSingleton(_ => new AzureOpenAIClient(new Uri(endpoint), credential));
        services.AddSingleton(sp => sp.GetRequiredService<AzureOpenAIClient>().GetChatClient(deploymentName));
        services.AddSingleton<ILeadAnalysisService, AzureOpenAiLeadAnalysisService>();

        services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateProcessor(serviceBusQueueName));

        return services;
    }
}
