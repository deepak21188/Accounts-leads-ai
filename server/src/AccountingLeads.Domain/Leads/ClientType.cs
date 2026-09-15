namespace AccountingLeads.Domain.Leads;

public enum ClientType
{
    Individual = 1,
    SoleProprietorship = 2,
    Corporation = 3,

    /// <summary>Displayed as "LLC" in the UI.</summary>
    LimitedLiabilityCompany = 4,
    Partnership = 5,
    NonProfit = 6,
    Other = 7
}
