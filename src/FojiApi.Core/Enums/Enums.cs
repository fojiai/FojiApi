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

/// <summary>Kind of CRM follow-up task. Stored as a string, so adding a member
/// needs no migration — but never rename one, that would orphan existing rows.</summary>
public enum CrmTaskType
{
    General,
    Call,
    Email,
    WhatsApp,
    Meeting,
    Presentation,
    Visit,
    FollowUp
}

public enum CrmTaskPriority
{
    Low,
    Normal,
    High
}

public enum CrmTaskStatus
{
    Open,
    Done
}

/// <summary>Who answers inbound WhatsApp messages for an agent's number.</summary>
public enum WhatsAppMode
{
    /// <summary>The AI agent replies automatically (the original behaviour).</summary>
    Agent,

    /// <summary>Messages land in the shared team inbox and the AI stays silent.</summary>
    Inbox
}

public enum MessageDirection
{
    Inbound,
    Outbound
}
