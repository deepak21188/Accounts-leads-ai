using AccountingLeads.Application;
using AccountingLeads.Infrastructure;
using AccountingLeads.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddOutboxProcessing();
builder.Services.AddLeadProcessing();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOutboxPublishing(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddLeadAnalysis(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<LeadProcessingWorker>();

var host = builder.Build();
host.Run();
