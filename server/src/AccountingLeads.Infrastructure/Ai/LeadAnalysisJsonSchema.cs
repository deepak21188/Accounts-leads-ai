namespace AccountingLeads.Infrastructure.Ai;

/// <summary>
/// Hand-written JSON Schema (not <c>System.Text.Json.Schema.JsonSchemaExporter</c>) for Azure
/// OpenAI's strict structured-output mode. Every real Microsoft-documented .NET example for
/// <c>ChatResponseFormat.CreateJsonSchemaFormat</c> hand-writes the schema string rather than
/// generating it via reflection — there's no official precedent for the exporter in this exact
/// scenario, and strict mode's specific requirements (every property in "required" regardless
/// of nullability, "additionalProperties": false on every object, nullable fields expressed as
/// a two-element "type" array) are easy to get subtly wrong via an untested transform against a
/// live API call. Keep this in sync by hand with <see cref="LeadAnalysisAiResponse"/> and
/// <c>AccountingServiceType</c>/<c>ClientType</c>/<c>Urgency</c> — there is no compile-time
/// check tying them together.
/// </summary>
internal static class LeadAnalysisJsonSchema
{
    public const string Value = """
        {
            "type": "object",
            "properties": {
                "RequestedServices": {
                    "type": "array",
                    "items": {
                        "type": "string",
                        "enum": ["Bookkeeping", "CatchUpBookkeeping", "CorporateTaxPreparation", "PersonalTaxPreparation", "Payroll", "Advisory", "AccountingSystemSetup", "Other"]
                    }
                },
                "ExtractedClientType": {
                    "type": ["string", "null"],
                    "enum": ["Individual", "SoleProprietorship", "Corporation", "LimitedLiabilityCompany", "Partnership", "NonProfit", "Other", null]
                },
                "ExtractedApproximateAnnualRevenue": {
                    "type": ["number", "null"]
                },
                "ExtractedAccountingSoftware": {
                    "type": ["string", "null"]
                },
                "Urgency": {
                    "type": ["string", "null"],
                    "enum": ["Low", "Medium", "High", null]
                },
                "ExtractedFilingDeadline": {
                    "type": ["string", "null"]
                },
                "Notes": {
                    "type": ["string", "null"]
                },
                "EstimatedDeadlineInDays": {
                    "type": ["integer", "null"]
                },
                "BookkeepingMonthsBehind": {
                    "type": ["integer", "null"]
                }
            },
            "required": ["RequestedServices", "ExtractedClientType", "ExtractedApproximateAnnualRevenue", "ExtractedAccountingSoftware", "Urgency", "ExtractedFilingDeadline", "Notes", "EstimatedDeadlineInDays", "BookkeepingMonthsBehind"],
            "additionalProperties": false
        }
        """;
}
