using AccountingLeads.Application.Leads;
using AccountingLeads.Application.Leads.ProcessLead;
using AccountingLeads.Application.Leads.Qualification;
using AccountingLeads.Domain.Leads;
using AccountingLeads.UnitTests.Application.TestDoubles;

namespace AccountingLeads.UnitTests.Application.Leads.ProcessLead;

/// <summary>
/// Unit tests for <see cref="ProcessLeadCommandHandler"/> against hand-written
/// <see cref="FakeApplicationDbContext"/>/<see cref="FakeLeadAnalysisService"/> doubles, per this
/// codebase's no-mocking-library convention.
/// </summary>
public class ProcessLeadCommandHandlerTests
{
    private static Lead CreateValidLead() => Lead.Create(
        "Jane Accountant",
        "jane@example.com",
        phone: null,
        companyName: null,
        clientType: null,
        approximateAnnualRevenue: null,
        accountingSoftware: null,
        inquiry: "I need help with catch-up bookkeeping for my small business.");

    [Fact]
    public async Task HandleAsync_WhenLeadDoesNotExist_ThrowsInvalidOperationException()
    {
        var dbContext = new FakeApplicationDbContext();
        var analysisService = new FakeLeadAnalysisService();
        var handler = new ProcessLeadCommandHandler(dbContext, analysisService, new LeadQualificationService());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new ProcessLeadCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WhenLeadAlreadyCompletedAndQualified_ReturnsAlreadyCompletedWithoutCallingAnalysisService()
    {
        var lead = CreateValidLead();
        lead.BeginAiProcessing();
        lead.CompleteAiProcessing();
        lead.MarkQualified();

        var dbContext = new FakeApplicationDbContext();
        dbContext.Leads.Add(lead);
        var analysisService = new FakeLeadAnalysisService();
        var handler = new ProcessLeadCommandHandler(dbContext, analysisService, new LeadQualificationService());

        var result = await handler.HandleAsync(new ProcessLeadCommand(lead.Id), CancellationToken.None);

        Assert.Equal(ProcessLeadOutcome.AlreadyCompleted, result.Outcome);
        Assert.Null(analysisService.LastAnalyzedLead);
        Assert.Empty(dbContext.AddedLeadAnalyses);
        Assert.Empty(dbContext.AddedLeadQualificationResults);
    }

    [Fact]
    public async Task HandleAsync_WhenCompletedButNeverQualified_BackfillsQualificationFromExistingAnalysisWithoutCallingAnalysisService()
    {
        // Represents a lead processed before Slice 4 (qualification) existed: AI analysis
        // already completed and a LeadAnalysis was already persisted, but the lead was never
        // qualified because ProcessLeadCommandHandler's older short-circuit skipped it outright.
        var lead = CreateValidLead();
        lead.BeginAiProcessing();
        lead.CompleteAiProcessing();

        var existingAnalysis = LeadAnalysis.Create(
            lead.Id,
            requestedServices: new[] { AccountingServiceType.Bookkeeping },
            extractedClientType: null,
            extractedApproximateAnnualRevenue: null,
            extractedAccountingSoftware: null,
            urgency: null,
            extractedFilingDeadline: null,
            notes: null,
            estimatedDeadlineInDays: null,
            bookkeepingMonthsBehind: null);

        var dbContext = new FakeApplicationDbContext();
        dbContext.Leads.Add(lead);
        dbContext.LeadAnalyses.Add(existingAnalysis);
        var analysisService = new FakeLeadAnalysisService();
        var handler = new ProcessLeadCommandHandler(dbContext, analysisService, new LeadQualificationService());

        var result = await handler.HandleAsync(new ProcessLeadCommand(lead.Id), CancellationToken.None);

        Assert.Equal(ProcessLeadOutcome.QualifiedFromExistingAnalysis, result.Outcome);
        Assert.Null(analysisService.LastAnalyzedLead);
        Assert.Equal(LeadStatus.Qualified, lead.LeadStatus);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        Assert.Single(dbContext.AddedLeadAnalyses); // only the pre-seeded one — the handler adds no second analysis
        var qualification = Assert.Single(dbContext.AddedLeadQualificationResults);
        Assert.Equal(lead.Id, qualification.LeadId);
        Assert.Equal(15, qualification.Score); // Bookkeeping alone, no other facts known
    }

    [Fact]
    public async Task HandleAsync_WhenCompletedButNeverQualifiedAndNoAnalysisExists_ThrowsInvalidOperationException()
    {
        var lead = CreateValidLead();
        lead.BeginAiProcessing();
        lead.CompleteAiProcessing();

        var dbContext = new FakeApplicationDbContext();
        dbContext.Leads.Add(lead);
        var analysisService = new FakeLeadAnalysisService();
        var handler = new ProcessLeadCommandHandler(dbContext, analysisService, new LeadQualificationService());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new ProcessLeadCommand(lead.Id), CancellationToken.None));

        Assert.Empty(dbContext.AddedLeadQualificationResults);
    }

    [Fact]
    public async Task HandleAsync_WhenAnalysisServiceThrows_MarksFailedSavesOnceAndRethrows()
    {
        var lead = CreateValidLead();
        var dbContext = new FakeApplicationDbContext();
        dbContext.Leads.Add(lead);
        var analysisService = new FakeLeadAnalysisService
        {
            ExceptionToThrow = new InvalidOperationException("Simulated Azure OpenAI failure.")
        };
        var handler = new ProcessLeadCommandHandler(dbContext, analysisService, new LeadQualificationService());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new ProcessLeadCommand(lead.Id), CancellationToken.None));

        Assert.Equal(AiProcessingStatus.Failed, lead.AiProcessingStatus);
        Assert.Equal(LeadStatus.New, lead.LeadStatus);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        Assert.Empty(dbContext.AddedLeadAnalyses);
        Assert.Empty(dbContext.AddedLeadQualificationResults);
    }

    [Fact]
    public async Task HandleAsync_WhenAnalysisSucceeds_PersistsAnalysisCompletesAndSavesOnce()
    {
        var lead = CreateValidLead();
        var dbContext = new FakeApplicationDbContext();
        dbContext.Leads.Add(lead);
        var analysisResult = new LeadAnalysisResult(
            RequestedServices: new[] { AccountingServiceType.Bookkeeping },
            ExtractedClientType: ClientType.SoleProprietorship,
            ExtractedApproximateAnnualRevenue: 100_000m,
            ExtractedAccountingSoftware: "QuickBooks",
            Urgency: Urgency.Medium,
            ExtractedFilingDeadline: "in about a month",
            Notes: "Wants ongoing bookkeeping.",
            EstimatedDeadlineInDays: 30,
            BookkeepingMonthsBehind: null);
        var analysisService = new FakeLeadAnalysisService { ResultToReturn = analysisResult };
        var handler = new ProcessLeadCommandHandler(dbContext, analysisService, new LeadQualificationService());

        var result = await handler.HandleAsync(new ProcessLeadCommand(lead.Id), CancellationToken.None);

        Assert.Equal(ProcessLeadOutcome.Processed, result.Outcome);
        Assert.Equal(AiProcessingStatus.Completed, lead.AiProcessingStatus);
        Assert.Equal(LeadStatus.Qualified, lead.LeadStatus);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        var persisted = Assert.Single(dbContext.AddedLeadAnalyses);
        Assert.Equal(lead.Id, persisted.LeadId);
        Assert.Equal(analysisResult.RequestedServices, persisted.RequestedServices);
        Assert.Same(lead, analysisService.LastAnalyzedLead);

        var qualification = Assert.Single(dbContext.AddedLeadQualificationResults);
        Assert.Equal(lead.Id, qualification.LeadId);
        // Bookkeeping (15) + revenue $100,000-$249,999 (10) + deadline within 30 days (20)
        // + 0 (no months-behind) + all 5 facts known (client type, revenue, software,
        // requested service, deadline -> completeness 5) = 50.
        Assert.Equal(50, qualification.Score);
        Assert.Equal(LeadPriority.Medium, qualification.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenAnalysisResultFailsDomainValidation_MarksFailedSavesOnceAndRethrows()
    {
        var lead = CreateValidLead();
        var dbContext = new FakeApplicationDbContext();
        dbContext.Leads.Add(lead);
        // Exceeds Lead.MaxApproximateAnnualRevenue, so LeadAnalysis.Create itself throws
        // DomainValidationException — this must be caught by the same failure path as an
        // AnalyzeAsync exception, not propagate past it uncaught.
        var analysisResult = new LeadAnalysisResult(
            RequestedServices: Array.Empty<AccountingServiceType>(),
            ExtractedClientType: null,
            ExtractedApproximateAnnualRevenue: Lead.MaxApproximateAnnualRevenue + 0.01m,
            ExtractedAccountingSoftware: null,
            Urgency: null,
            ExtractedFilingDeadline: null,
            Notes: null,
            EstimatedDeadlineInDays: null,
            BookkeepingMonthsBehind: null);
        var analysisService = new FakeLeadAnalysisService { ResultToReturn = analysisResult };
        var handler = new ProcessLeadCommandHandler(dbContext, analysisService, new LeadQualificationService());

        await Assert.ThrowsAsync<AccountingLeads.Domain.Exceptions.DomainValidationException>(
            () => handler.HandleAsync(new ProcessLeadCommand(lead.Id), CancellationToken.None));

        Assert.Equal(AiProcessingStatus.Failed, lead.AiProcessingStatus);
        Assert.Equal(LeadStatus.New, lead.LeadStatus);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        Assert.Empty(dbContext.AddedLeadAnalyses);
        Assert.Empty(dbContext.AddedLeadQualificationResults);
    }

    [Fact]
    public async Task HandleAsync_WhenRetriedAfterPreviousFailure_ReprocessesSuccessfully()
    {
        var lead = CreateValidLead();
        lead.BeginAiProcessing();
        lead.FailAiProcessing();

        var dbContext = new FakeApplicationDbContext();
        dbContext.Leads.Add(lead);
        var analysisService = new FakeLeadAnalysisService();
        var handler = new ProcessLeadCommandHandler(dbContext, analysisService, new LeadQualificationService());

        var result = await handler.HandleAsync(new ProcessLeadCommand(lead.Id), CancellationToken.None);

        Assert.Equal(ProcessLeadOutcome.Processed, result.Outcome);
        Assert.Equal(AiProcessingStatus.Completed, lead.AiProcessingStatus);
        Assert.Equal(LeadStatus.Qualified, lead.LeadStatus);
        Assert.Single(dbContext.AddedLeadAnalyses);
        Assert.Single(dbContext.AddedLeadQualificationResults);
    }
}
