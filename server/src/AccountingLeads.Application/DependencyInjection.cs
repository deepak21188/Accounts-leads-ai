using AccountingLeads.Application.Common.Messaging;
using AccountingLeads.Application.Leads.CreateLead;
using AccountingLeads.Application.Leads.GetLeadDetail;
using AccountingLeads.Application.Leads.GetLeads;
using AccountingLeads.Application.Leads.ProcessLead;
using AccountingLeads.Application.Leads.Qualification;
using AccountingLeads.Application.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingLeads.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateLeadCommandHandler>();
        services.AddScoped<GetLeadsQueryHandler>();
        services.AddScoped<GetLeadDetailQueryHandler>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="OutboxPublicationService"/>. Deliberately separate from
    /// <see cref="AddApplication"/> and called only by the Worker — its <see cref="IOutboxPublisher"/>
    /// dependency (registered by Infrastructure's <c>AddOutboxPublishing</c>) is never
    /// registered for the Api, and ASP.NET Core's dev-time ValidateOnBuild validates every
    /// registered service's dependency graph regardless of whether anything ever resolves it,
    /// so registering this in the shared AddApplication would break Api startup.
    /// </summary>
    public static IServiceCollection AddOutboxProcessing(this IServiceCollection services)
    {
        services.AddScoped<OutboxPublicationService>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="ProcessLeadCommandHandler"/>. Deliberately separate from
    /// <see cref="AddApplication"/> and called only by the Worker, for the same
    /// ValidateOnBuild-isolation reason as <see cref="AddOutboxProcessing"/> — its
    /// <see cref="AccountingLeads.Application.Leads.ILeadAnalysisService"/> dependency
    /// (registered by Infrastructure's <c>AddLeadAnalysis</c>) is never registered for the Api.
    /// </summary>
    public static IServiceCollection AddLeadProcessing(this IServiceCollection services)
    {
        services.AddScoped<LeadQualificationService>();
        services.AddScoped<ProcessLeadCommandHandler>();

        return services;
    }
}
