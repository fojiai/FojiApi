using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Asaas;

/// <summary>
/// Thin typed client for the Asaas v3 API (docs.asaas.com, checked 2026-10).
///
/// Rules that matter:
///  - Auth is the <c>access_token</c> header; a User-Agent is mandatory for accounts
///    created after 13/06/2024.
///  - POSTs are never retried automatically: a timed-out create may have succeeded,
///    and Asaas happily creates duplicates.
///  - Dates are plain Brasília dates (yyyy-MM-dd).
/// </summary>
public class AsaasClient(HttpClient http, IConfiguration configuration, ILogger<AsaasClient> logger)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public const string DefaultBaseUrl = "https://api-sandbox.asaas.com/v3";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Asaas:ApiKey"]);

    /// <summary>Card tokenization is enabled on the Asaas account (production needs the account manager to turn it on).</summary>
    public bool TokenizationEnabled => configuration.GetValue("Asaas:TokenizationEnabled", false);

    private static readonly Lazy<string> ItemImage = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("FojiApi.Infrastructure.Asaas.checkout-item.b64");
        return stream is null ? string.Empty : new StreamReader(stream).ReadToEnd().Trim();
    });

    // ── Customers ────────────────────────────────────────────────────────────

    public async Task<AsaasCustomer?> FindCustomerByReferenceAsync(string externalReference, CancellationToken ct = default)
    {
        var page = await SendAsync<AsaasList<AsaasCustomer>>(HttpMethod.Get,
            $"customers?externalReference={Uri.EscapeDataString(externalReference)}&limit=10", null, ct);
        return page?.Data.FirstOrDefault(c => !c.Deleted);
    }

    public Task<AsaasCustomer> CreateCustomerAsync(AsaasCustomerRequest request, CancellationToken ct = default) =>
        SendRequiredAsync<AsaasCustomer>(HttpMethod.Post, "customers", request, ct);

    public Task<AsaasCustomer> UpdateCustomerAsync(string id, AsaasCustomerRequest request, CancellationToken ct = default) =>
        SendRequiredAsync<AsaasCustomer>(HttpMethod.Put, $"customers/{id}", request, ct);

    // ── Hosted checkout (card subscriptions, no card data on our side) ────────

    public Task<AsaasCheckout> CreateCheckoutAsync(AsaasCheckoutRequest request, CancellationToken ct = default)
    {
        foreach (var item in request.Items)
            item.ImageBase64 ??= ItemImage.Value is { Length: > 0 } img ? img : null;
        return SendRequiredAsync<AsaasCheckout>(HttpMethod.Post, "checkouts", request, ct);
    }

    public Task CancelCheckoutAsync(string id, CancellationToken ct = default) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"checkouts/{id}/cancel", new { }, ct);

    // ── Subscriptions ────────────────────────────────────────────────────────

    public Task<AsaasSubscription> CreateSubscriptionAsync(AsaasSubscriptionRequest request, CancellationToken ct = default) =>
        SendRequiredAsync<AsaasSubscription>(HttpMethod.Post, "subscriptions", request, ct);

    public Task<AsaasSubscription?> GetSubscriptionAsync(string id, CancellationToken ct = default) =>
        SendAsync<AsaasSubscription>(HttpMethod.Get, $"subscriptions/{id}", null, ct);

    /// <summary>
    /// Changes the price. <c>updatePendingPayments</c> matters: Asaas creates each
    /// charge up to 40 days early, so the next one usually exists already.
    /// </summary>
    public Task UpdateSubscriptionValueAsync(string id, decimal value, string? description, CancellationToken ct = default) =>
        SendRequiredAsync<AsaasSubscription>(HttpMethod.Put, $"subscriptions/{id}",
            new { value, description, updatePendingPayments = true }, ct);

    /// <summary>Ends the subscription and removes its pending/overdue charges (paid ones stay).</summary>
    public async Task DeleteSubscriptionAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await SendAsync<JsonElement>(HttpMethod.Delete, $"subscriptions/{id}", null, ct);
        }
        catch (AsaasException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            // Already gone: that's the outcome we wanted.
        }
    }

    public async Task<IReadOnlyList<AsaasPayment>> ListSubscriptionPaymentsAsync(string id, CancellationToken ct = default) =>
        (await SendAsync<AsaasList<AsaasPayment>>(HttpMethod.Get, $"subscriptions/{id}/payments?limit=20", null, ct))?.Data
        ?? [];

    public Task SetSubscriptionInvoiceSettingsAsync(string id, object settings, CancellationToken ct = default) =>
        SendRequiredAsync<JsonElement>(HttpMethod.Post, $"subscriptions/{id}/invoiceSettings", settings, ct);

    // ── One-off payments (upgrade difference, WhatsApp overage) ──────────────

    public Task<AsaasPayment> CreatePaymentAsync(AsaasPaymentRequest request, CancellationToken ct = default) =>
        SendRequiredAsync<AsaasPayment>(HttpMethod.Post, "payments", request, ct);

    public Task<AsaasPayment?> GetPaymentAsync(string id, CancellationToken ct = default) =>
        SendAsync<AsaasPayment>(HttpMethod.Get, $"payments/{id}", null, ct);

    /// <summary>Schedules an NFS-e for a one-off payment.</summary>
    public Task ScheduleInvoiceAsync(object invoice, CancellationToken ct = default) =>
        SendRequiredAsync<JsonElement>(HttpMethod.Post, "invoices", invoice, ct);

    // ── Plumbing ─────────────────────────────────────────────────────────────

    private async Task<T> SendRequiredAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct) =>
        await SendAsync<T>(method, path, body, ct)
        ?? throw new AsaasException(HttpStatusCode.NoContent, "empty_response", "Asaas returned an empty response.");

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var apiKey = configuration["Asaas:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Asaas:ApiKey is not configured.");

        var baseUrl = (configuration["Asaas:BaseUrl"] ?? DefaultBaseUrl).TrimEnd('/');
        using var req = new HttpRequestMessage(method, $"{baseUrl}/{path}");
        req.Headers.TryAddWithoutValidation("access_token", apiKey);
        req.Headers.TryAddWithoutValidation("User-Agent", "FojiAI/1.0 (.NET)");
        if (body is not null) req.Content = JsonContent.Create(body, body.GetType(), options: Json);

        using var resp = await http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);

        if (resp.StatusCode == HttpStatusCode.NotFound && method == HttpMethod.Get)
            return default;

        if (!resp.IsSuccessStatusCode)
        {
            var (code, description) = ParseError(text);
            logger.LogWarning("Asaas {Method} {Path} failed: {Status} {Code} {Description}",
                method, path, (int)resp.StatusCode, code, description);
            throw new AsaasException(resp.StatusCode, code, description);
        }

        return string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<T>(text, Json);
    }

    private static (string Code, string Description) ParseError(string text)
    {
        try
        {
            var err = JsonSerializer.Deserialize<AsaasErrorBody>(text, Json)?.Errors?.FirstOrDefault();
            if (err is not null) return (err.Code ?? "error", err.Description ?? text);
        }
        catch (JsonException) { }
        return ("error", text.Length > 300 ? text[..300] : text);
    }

    private sealed record AsaasErrorBody(List<AsaasError>? Errors);
    private sealed record AsaasError(string? Code, string? Description);
}

public class AsaasException(HttpStatusCode status, string code, string description)
    : Exception($"Asaas error {(int)status} {code}: {description}")
{
    public HttpStatusCode Status { get; } = status;
    public string Code { get; } = code;
    public string Description { get; } = description;
}

// ── DTOs (only the fields we use; unknown fields are ignored) ────────────────

public sealed record AsaasList<T>(List<T> Data, bool HasMore);

public sealed record AsaasCustomer(string Id, string? Name, string? CpfCnpj, string? Email, string? ExternalReference, bool Deleted);

public sealed record AsaasCustomerRequest(
    string Name, string CpfCnpj, string? Email, string? ExternalReference, bool NotificationDisabled = true);

public sealed class AsaasCheckoutRequest
{
    public List<string> BillingTypes { get; init; } = ["CREDIT_CARD"];
    public List<string> ChargeTypes { get; init; } = ["RECURRENT"];
    public int MinutesToExpire { get; init; } = 60;
    public string? ExternalReference { get; init; }
    public string? Customer { get; init; }
    public required AsaasCallback Callback { get; init; }
    public required List<AsaasCheckoutItem> Items { get; init; }
    public AsaasCheckoutSubscription? Subscription { get; init; }
}

public sealed record AsaasCallback(string SuccessUrl, string? CancelUrl = null, string? ExpiredUrl = null, bool? AutoRedirect = null);

public sealed class AsaasCheckoutItem
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int Quantity { get; init; } = 1;
    public required decimal Value { get; init; }
    public string? ImageBase64 { get; set; }
}

public sealed record AsaasCheckoutSubscription(string Cycle, string NextDueDate, string? EndDate = null);

public sealed record AsaasCheckout(string Id, string? Link, string? Status);

public sealed record AsaasSubscriptionRequest(
    string Customer,
    string BillingType,
    decimal Value,
    string NextDueDate,
    string Cycle,
    string? Description,
    string? ExternalReference,
    AsaasCallback? Callback = null,
    string? CreditCardToken = null,
    string? RemoteIp = null);

public sealed record AsaasSubscription(
    string Id,
    string? Customer,
    string? BillingType,
    decimal Value,
    string? Cycle,
    string? Status,
    string? NextDueDate,
    string? ExternalReference,
    string? CheckoutSession,
    bool Deleted);

public sealed record AsaasPaymentRequest(
    string Customer,
    string BillingType,
    decimal Value,
    string DueDate,
    string? Description,
    string? ExternalReference,
    AsaasCallback? Callback = null,
    string? CreditCardToken = null,
    string? RemoteIp = null);

public sealed record AsaasPayment(
    string Id,
    string? Customer,
    string? Subscription,
    string? Status,
    string? BillingType,
    decimal Value,
    string? DueDate,
    string? PaymentDate,
    string? ConfirmedDate,
    string? ClientPaymentDate,
    string? InvoiceUrl,
    string? Description,
    string? ExternalReference,
    string? CheckoutSession,
    AsaasCreditCardInfo? CreditCard,
    bool Deleted);

public sealed record AsaasCreditCardInfo(string? CreditCardNumber, string? CreditCardBrand, string? CreditCardToken);
