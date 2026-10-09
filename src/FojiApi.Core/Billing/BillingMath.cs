using FojiApi.Core.Enums;

namespace FojiApi.Core.Billing;

/// <summary>Billing rules with no I/O, so they can be tested on their own.</summary>
public static class BillingMath
{
    /// <summary>
    /// Asaas works in Brasília dates (no time zone on due dates). Brazil has had no
    /// daylight saving since 2019, so a fixed -03:00 is exact and needs no tzdata.
    /// </summary>
    public static readonly TimeSpan BrasiliaOffset = TimeSpan.FromHours(-3);

    public static DateOnly TodayInBrasilia(DateTime utcNow) =>
        DateOnly.FromDateTime(utcNow.Add(BrasiliaOffset));

    /// <summary>Midnight in Brasília of that date, as UTC.</summary>
    public static DateTime StartOfDayUtc(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Subtract(BrasiliaOffset);

    public static DateOnly AddCycle(DateOnly date, BillingCycle cycle) =>
        cycle == BillingCycle.Yearly ? date.AddYears(1) : date.AddMonths(1);

    public static string ToAsaasCycle(BillingCycle cycle) =>
        cycle == BillingCycle.Yearly ? "YEARLY" : "MONTHLY";

    public static decimal PriceFor(decimal monthly, decimal? yearly, BillingCycle cycle) =>
        cycle == BillingCycle.Yearly
            ? yearly ?? throw new InvalidOperationException("Plan has no yearly price.")
            : monthly;

    /// <summary>
    /// What an upgrade costs right now: the price difference for the part of the
    /// paid period that is left, rounded to centavos. Never negative (downgrades
    /// wait for the period end and are not refunded).
    /// </summary>
    public static decimal ProratedUpgrade(
        decimal oldPrice, decimal newPrice, DateTime periodStartUtc, DateTime periodEndUtc, DateTime nowUtc)
    {
        var diff = newPrice - oldPrice;
        if (diff <= 0) return 0m;

        var total = (periodEndUtc - periodStartUtc).TotalSeconds;
        if (total <= 0) return 0m;

        var left = Math.Clamp((periodEndUtc - nowUtc).TotalSeconds, 0, total);
        return Math.Round(diff * (decimal)(left / total), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The monthly WhatsApp usage window containing <paramref name="at"/>, anchored
    /// on the day the subscription started (so a yearly plan still gets a monthly
    /// allowance). End is exclusive.
    /// </summary>
    public static (DateOnly Start, DateOnly End) UsageWindow(DateOnly anchor, DateOnly at)
    {
        // Always count from the anchor (also backwards) so a 31st anchor stays on the 31st when it can.
        var months = (at.Year - anchor.Year) * 12 + at.Month - anchor.Month;
        if (anchor.AddMonths(months) > at) months--;
        return (anchor.AddMonths(months), anchor.AddMonths(months + 1));
    }

    /// <summary>Overage owed for a window, in BRL.</summary>
    public static decimal Overage(int used, int allowance, int overageCentavos) =>
        allowance < 0 || overageCentavos <= 0 || used <= allowance
            ? 0m
            : (used - allowance) * overageCentavos / 100m;

    public static string DigitsOnly(string? value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>Checks the CPF (11 digits) or CNPJ (14 digits) check digits.</summary>
    public static bool IsValidCpfCnpj(string? value)
    {
        var d = DigitsOnly(value);
        return d.Length switch
        {
            11 => IsValidCpf(d),
            14 => IsValidCnpj(d),
            _ => false,
        };
    }

    private static bool IsValidCpf(string d)
    {
        if (d.Distinct().Count() == 1) return false;
        int Digit(int len)
        {
            var sum = 0;
            for (var i = 0; i < len; i++) sum += (d[i] - '0') * (len + 1 - i);
            var r = sum * 10 % 11;
            return r == 10 ? 0 : r;
        }
        return Digit(9) == d[9] - '0' && Digit(10) == d[10] - '0';
    }

    private static bool IsValidCnpj(string d)
    {
        if (d.Distinct().Count() == 1) return false;
        int[] w1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] w2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int Digit(int[] w)
        {
            var sum = 0;
            for (var i = 0; i < w.Length; i++) sum += (d[i] - '0') * w[i];
            var r = sum % 11;
            return r < 2 ? 0 : 11 - r;
        }
        return Digit(w1) == d[12] - '0' && Digit(w2) == d[13] - '0';
    }
}
