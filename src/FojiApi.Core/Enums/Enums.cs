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
    /// <summary>A charge is overdue. Still served during the grace period (Billing:GraceDays).</summary>
    PastDue,
    Canceled,
    /// <summary>Overdue past the grace period: features locked until the customer pays.</summary>
    Unpaid,
    /// <summary>Created at Asaas but the first payment hasn't been confirmed yet. Never served.</summary>
    Incomplete
}

public enum BillingCycle
{
    Monthly,
    Yearly
}

/// <summary>How a subscription is paid. Pix is only offered on yearly plans.</summary>
public enum BillingPaymentMethod
{
    CreditCard,
    Pix,
    /// <summary>Assigned by a super-admin (invoiced outside the app).</summary>
    Manual
}

public enum BillingPaymentKind
{
    /// <summary>A recurring charge of the subscription.</summary>
    Subscription,
    /// <summary>The prorated difference when upgrading mid-period.</summary>
    Upgrade,
    /// <summary>WhatsApp messages beyond the plan's allowance.</summary>
    Overage
}

public enum BillingPaymentStatus
{
    Pending,
    Confirmed,
    Received,
    Overdue,
    Refunded,
    Deleted,
    /// <summary>Below the minimum chargeable value; recorded but not charged.</summary>
    Waived
}

public enum BillingCheckoutKind
{
    /// <summary>A new subscription (from trial, no plan or canceled).</summary>
    NewSubscription,
    /// <summary>Prorated upgrade payment on an existing subscription.</summary>
    Upgrade,
    /// <summary>A subscription that replaces the current one (new card, new cycle, resume).</summary>
    Replacement
}

public enum BillingCheckoutStatus
{
    Pending,
    Completed,
    Expired,
    Canceled
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
    Inbox,

    /// <summary>
    /// The AI answers and every conversation also lands in the inbox, where a
    /// person can take it over — the AI goes quiet in that conversation until
    /// it's handed back or resolved. The AI calls the team itself when needed.
    /// </summary>
    Hybrid
}

public enum MessageDirection
{
    Inbound,
    Outbound
}

/// <summary>
/// Whether a shared-inbox conversation still needs the team. A new inbound
/// message always reopens it.
/// </summary>
public enum InboxConversationStatus
{
    Open,
    Resolved
}
