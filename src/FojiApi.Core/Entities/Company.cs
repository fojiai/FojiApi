using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

public class Company : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional trading/fantasy name. For Pessoa Física this is their full legal name;
    /// for Pessoa Jurídica this may differ from the registered company name.
    /// </summary>
    public string? TradeName { get; set; }

    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? StripeCustomerId { get; set; }

    /// <summary>Business (Pessoa Jurídica) or Individual (Pessoa Física).</summary>
    public AccountType AccountType { get; set; } = AccountType.Business;

    /// <summary>
    /// CPF (11 digits) for Pessoa Física, CNPJ (14 digits) for Pessoa Jurídica.
    /// Stored digits-only, no formatting.
    /// </summary>
    public string? CpfCnpj { get; set; }

    /// <summary>
    /// Admin notes — internal observations about this account (billing arrangements, custom deals, etc.).
    /// </summary>
    public string? AdminNotes { get; set; }

    // Navigation
    public ICollection<UserCompany> UserCompanies { get; set; } = [];
    public ICollection<Agent> Agents { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<Invitation> Invitations { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<DailyStat> DailyStats { get; set; } = [];
}
