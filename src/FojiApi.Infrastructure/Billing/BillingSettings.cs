using Microsoft.Extensions.Configuration;

namespace FojiApi.Infrastructure.Billing;

/// <summary>Billing knobs, all overridable from configuration (SSM in AWS).</summary>
public class BillingSettings(IConfiguration configuration)
{
    public string AppBaseUrl => (configuration["App:BaseUrl"] ?? "https://app.fojiai.com").TrimEnd('/');

    /// <summary>
    /// Where Asaas sends the customer back after paying. Asaas only accepts URLs on
    /// the domain registered in the account's commercial info, so this can point at
    /// a different host than the app (Asaas:ReturnBaseUrl).
    /// </summary>
    public string ReturnBaseUrl => (configuration["Asaas:ReturnBaseUrl"] ?? AppBaseUrl).TrimEnd('/');

    public bool EnforcementEnabled => configuration.GetValue<bool?>("Billing:EnforcementEnabled") ?? true;

    /// <summary>Days an overdue subscription keeps working before features lock.</summary>
    public int GraceDays => configuration.GetValue("Billing:GraceDays", 7);

    /// <summary>Days after the first overdue charge before we give up and cancel at Asaas.</summary>
    public int CancelAfterDays => configuration.GetValue("Billing:CancelAfterDays", 30);

    /// <summary>Upgrades/overage below this are applied/waived without a charge (Asaas has minimums and fees).</summary>
    public decimal MinChargeValue => configuration.GetValue("Billing:MinChargeValue", 5m);

    /// <summary>Prefix on every externalReference so a shared Asaas account can tell products apart.</summary>
    public string ReferencePrefix => configuration["Asaas:ReferencePrefix"] ?? "foji";

    public string WebhookToken => configuration["Asaas:WebhookToken"] ?? string.Empty;

    public bool NfseEnabled => configuration.GetValue("Asaas:Nfse:Enabled", false);

    public string CheckoutReference(int checkoutId) => $"{ReferencePrefix}:chk:{checkoutId}";
    public string OverageReference(int paymentId) => $"{ReferencePrefix}:ovg:{paymentId}";
    public string CustomerReference(int companyId) => $"{ReferencePrefix}:company:{companyId}";

    public int? ParseCheckoutReference(string? reference) => ParseId(reference, $"{ReferencePrefix}:chk:");
    public int? ParseOverageReference(string? reference) => ParseId(reference, $"{ReferencePrefix}:ovg:");

    private static int? ParseId(string? reference, string prefix) =>
        reference is not null && reference.StartsWith(prefix, StringComparison.Ordinal)
        && int.TryParse(reference[prefix.Length..], out var id) ? id : null;

    /// <summary>
    /// NFS-e tax settings sent to Asaas (Asaas:Nfse:*). The codes and rates depend on
    /// the city and tax regime of the issuing company, so they come from config.
    /// </summary>
    public object NfseTaxes => new
    {
        retainIss = configuration.GetValue("Asaas:Nfse:RetainIss", false),
        iss = configuration.GetValue("Asaas:Nfse:Iss", 0m),
        pis = configuration.GetValue("Asaas:Nfse:Pis", 0m),
        cofins = configuration.GetValue("Asaas:Nfse:Cofins", 0m),
        csll = configuration.GetValue("Asaas:Nfse:Csll", 0m),
        inss = configuration.GetValue("Asaas:Nfse:Inss", 0m),
        ir = configuration.GetValue("Asaas:Nfse:Ir", 0m),
    };

    public string? NfseServiceCode => configuration["Asaas:Nfse:MunicipalServiceCode"];
    public string? NfseServiceName => configuration["Asaas:Nfse:MunicipalServiceName"];
    public string NfseDescription => configuration["Asaas:Nfse:ServiceDescription"]
        ?? "Assinatura da plataforma Foji AI (atendente virtual com inteligência artificial)";
}
