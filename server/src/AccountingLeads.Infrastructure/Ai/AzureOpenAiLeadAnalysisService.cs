using System.Text.Json;
using AccountingLeads.Application.Leads;
using AccountingLeads.Domain.Leads;
using OpenAI.Chat;

namespace AccountingLeads.Infrastructure.Ai;

/// <summary>
/// Implements <see cref="ILeadAnalysisService"/> against Azure OpenAI's chat-completions API in
/// strict structured-output mode. The <see cref="ChatClient"/> passed in is resolved via
/// <c>Azure.AI.OpenAI.AzureOpenAIClient.GetChatClient</c> (see
/// <c>DependencyInjection.AddLeadAnalysis</c>) — this class only depends on the standard
/// <c>OpenAI.Chat</c> types, not on anything Azure-specific, so a future provider swap only
/// touches the DI registration.
/// </summary>
public sealed class AzureOpenAiLeadAnalysisService : ILeadAnalysisService
{
    private static readonly JsonSerializerOptions ResponseSerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly ChatCompletionOptions CompletionOptions = new()
    {
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "lead_analysis",
            jsonSchema: BinaryData.FromString(LeadAnalysisJsonSchema.Value),
            jsonSchemaFormatDescription: "Structured extraction of accounting-lead facts from a prospective client's inquiry.",
            jsonSchemaIsStrict: true)
    };

    private const string SystemPrompt = """
        You are an assistant for an accounting firm's lead-intake process. Read a prospective
        client's inquiry and extract the structured facts it contains. Only extract what the
        inquiry actually states or clearly implies — do not guess or invent details. Leave a
        field null (or RequestedServices empty) when the inquiry does not address it. The
        client's own submitted client type, annual revenue, and accounting software are provided
        for context; only fill the corresponding Extracted* field if the inquiry itself adds or
        clarifies that information, otherwise leave it null.

        Also estimate, when the inquiry gives enough information: EstimatedDeadlineInDays, a
        whole number of days from the submission date given below (treat it as "today") until any
        filing or work deadline the client mentions — for a relative phrase (e.g. "in about three
        weeks" → approximately 21) count forward from that date, for an absolute date (e.g.
        "September 20, 2026") compute the actual day difference from that date, and for a
        deadline already in the past relative to it (e.g. "was due three days ago") return a
        negative number (e.g. -3) rather than treating it as an error. Also estimate
        BookkeepingMonthsBehind, a whole number of months of bookkeeping backlog if the inquiry
        mentions being behind (e.g. "about six months behind" → 6). Leave either field null if
        the inquiry does not give enough information to estimate it — do not guess a number from
        nothing.
        """;

    private readonly ChatClient _chatClient;

    public AzureOpenAiLeadAnalysisService(ChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<LeadAnalysisResult> AnalyzeAsync(Lead lead, CancellationToken cancellationToken)
    {
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(BuildUserPrompt(lead))
        };

        var response = await _chatClient.CompleteChatAsync(messages, CompletionOptions, cancellationToken);
        var completion = response.Value;
        var json = completion.Content[0].Text;

        var aiResponse = JsonSerializer.Deserialize<LeadAnalysisAiResponse>(json, ResponseSerializerOptions)
            ?? throw new InvalidOperationException("Azure OpenAI returned an empty lead analysis response.");

        return MapToResult(aiResponse);
    }

    private static string BuildUserPrompt(Lead lead)
    {
        // The lead's own submission date anchors "today" for EstimatedDeadlineInDays — using the
        // wall-clock time of this AI call instead would make the same inquiry score differently
        // depending on how long it sat in the queue before being processed or retried.
        return $"""
            Today's date (the date this inquiry was submitted): {lead.CreatedAtUtc:yyyy-MM-dd}

            Client-submitted context:
            - Client type: {lead.ClientType?.ToString() ?? "not provided"}
            - Approximate annual revenue: {lead.ApproximateAnnualRevenue?.ToString() ?? "not provided"}
            - Accounting software: {lead.AccountingSoftware ?? "not provided"}

            Inquiry:
            {lead.Inquiry}
            """;
    }

    private static LeadAnalysisResult MapToResult(LeadAnalysisAiResponse aiResponse)
    {
        var requestedServices = aiResponse.RequestedServices
            .Select(value => Enum.TryParse<AccountingServiceType>(value, out var parsed) ? parsed : (AccountingServiceType?)null)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        var extractedClientType = Enum.TryParse<ClientType>(aiResponse.ExtractedClientType, out var clientType)
            ? clientType
            : (ClientType?)null;

        var urgency = Enum.TryParse<Urgency>(aiResponse.Urgency, out var parsedUrgency)
            ? parsedUrgency
            : (Urgency?)null;

        return new LeadAnalysisResult(
            requestedServices,
            extractedClientType,
            aiResponse.ExtractedApproximateAnnualRevenue,
            aiResponse.ExtractedAccountingSoftware,
            urgency,
            aiResponse.ExtractedFilingDeadline,
            aiResponse.Notes,
            aiResponse.EstimatedDeadlineInDays,
            aiResponse.BookkeepingMonthsBehind);
    }
}
