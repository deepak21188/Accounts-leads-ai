namespace AccountingLeads.Application.Leads.ProcessLead;

public enum ProcessLeadOutcome
{
    Processed,
    AlreadyCompleted,

    /// <summary>
    /// AI analysis had already completed (e.g. a lead processed before qualification existed)
    /// but the lead had never been qualified. Qualification ran against the existing
    /// <see cref="AccountingLeads.Domain.Leads.LeadAnalysis"/> without a new AI call.
    /// </summary>
    QualifiedFromExistingAnalysis
}

public sealed class ProcessLeadResult
{
    public ProcessLeadOutcome Outcome { get; }

    private ProcessLeadResult(ProcessLeadOutcome outcome)
    {
        Outcome = outcome;
    }

    public static ProcessLeadResult Processed() => new(ProcessLeadOutcome.Processed);

    public static ProcessLeadResult AlreadyCompleted() => new(ProcessLeadOutcome.AlreadyCompleted);

    public static ProcessLeadResult QualifiedFromExistingAnalysis() => new(ProcessLeadOutcome.QualifiedFromExistingAnalysis);
}
