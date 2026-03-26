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
    InternalSystems
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
