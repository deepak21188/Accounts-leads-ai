namespace AccountingLeads.Application.Leads.CreateLead;

public sealed class CreateLeadResult
{
    public bool IsSuccess { get; }
    public Guid? LeadId { get; }
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    private CreateLeadResult(bool isSuccess, Guid? leadId, IReadOnlyDictionary<string, string[]> validationErrors)
    {
        IsSuccess = isSuccess;
        LeadId = leadId;
        ValidationErrors = validationErrors;
    }

    public static CreateLeadResult Success(Guid leadId) =>
        new(true, leadId, new Dictionary<string, string[]>());

    public static CreateLeadResult Failure(string field, string error) =>
        new(false, null, new Dictionary<string, string[]> { [field] = new[] { error } });
}
