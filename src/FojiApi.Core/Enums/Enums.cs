namespace FojiApi.Core.Enums;

public enum CompanyRole
{
    Owner,
    Admin,
    User
}

public enum IndustryType
{
    AccountingFinance,
    Law,
    InternalSystems,
    GeneralAssistant
}

public enum AgentLanguage
{
    PtBr,
    En,
    Es
}

public enum FileProcessingStatus
{
    Pending,
    Processing,
    Ready,
    Failed
}

public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Canceled,
    Unpaid
}

public enum AiProvider
{
    OpenAi,
    Gemini,
    Bedrock
}

/// <summary>Pessoa Física (individual) or Pessoa Jurídica (business entity).</summary>
public enum AccountType
{
    Business,    // Pessoa Jurídica — CNPJ
    Individual   // Pessoa Física  — CPF
}

/// <summary>Lifecycle stage of a CRM contact.</summary>
public enum ContactStatus
{
    New,
    Open,
    Qualified,
    Customer,
    Unqualified,
    Archived
}

/// <summary>Status of a CRM deal/opportunity (denormalized from the stage's IsWon/IsLost).</summary>
public enum DealStatus
{
    Open,
    Won,
    Lost
}
