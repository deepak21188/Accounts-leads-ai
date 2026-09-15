namespace AccountingLeads.Infrastructure.Ai;

/// <summary>
/// Shape of the raw JSON the model returns, constrained by <see cref="LeadAnalysisJsonSchema"/>.
/// Enum-like fields are plain strings here (matching the schema's own string+enum constraint)
/// and are parsed into the real Domain enums in <c>AzureOpenAiLeadAnalysisService.MapToResult</c>
/// rather than deserializing directly into them, so an unexpected/unparseable value degrades to
/// null/empty instead of throwing during deserialization.
/// </summary>
internal sealed record LeadAnalysisAiResponse(
    string[] RequestedServices,
    string? ExtractedClientType,
    decimal? ExtractedApproximateAnnualRevenue,
    string? ExtractedAccountingSoftware,
    string? Urgency,
    string? ExtractedFilingDeadline,
    string? Notes,
    int? EstimatedDeadlineInDays,
    int? BookkeepingMonthsBehind);
