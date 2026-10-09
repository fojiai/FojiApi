using FojiApi.Core.Billing;
using FojiApi.Core.Enums;

namespace FojiApi.Tests;

public class BillingMathTests
{
    private static DateTime Utc(int y, int m, int d, int h = 0) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Upgrade_halfway_through_the_month_costs_half_the_difference()
    {
        var start = Utc(2026, 10, 1);
        var end = Utc(2026, 10, 31);
        var amount = BillingMath.ProratedUpgrade(100m, 200m, start, end, Utc(2026, 10, 16));
        Assert.Equal(50m, amount);
    }

    [Fact]
    public void Upgrade_on_the_first_day_costs_the_whole_difference()
    {
        var amount = BillingMath.ProratedUpgrade(99.90m, 199.90m, Utc(2026, 10, 1), Utc(2026, 11, 1), Utc(2026, 10, 1));
        Assert.Equal(100m, amount);
    }

    [Theory]
    [InlineData(200, 100)] // downgrade
    [InlineData(100, 100)] // same price
    public void No_charge_when_the_new_plan_is_not_more_expensive(decimal oldPrice, decimal newPrice)
    {
        Assert.Equal(0m, BillingMath.ProratedUpgrade(oldPrice, newPrice, Utc(2026, 10, 1), Utc(2026, 11, 1), Utc(2026, 10, 10)));
    }

    [Fact]
    public void Upgrade_after_the_period_ended_costs_nothing()
    {
        Assert.Equal(0m, BillingMath.ProratedUpgrade(100m, 200m, Utc(2026, 10, 1), Utc(2026, 11, 1), Utc(2026, 11, 5)));
    }

    [Fact]
    public void Proration_rounds_to_centavos()
    {
        var amount = BillingMath.ProratedUpgrade(0m, 100m, Utc(2026, 10, 1), Utc(2026, 10, 4), Utc(2026, 10, 2));
        Assert.Equal(66.67m, amount);
    }

    [Theory]
    [InlineData("2026-01-31", "2026-03-01", "2026-02-28", "2026-03-31")]
    [InlineData("2026-03-15", "2026-03-15", "2026-03-15", "2026-04-15")]
    [InlineData("2026-03-15", "2026-04-14", "2026-03-15", "2026-04-15")]
    [InlineData("2026-03-15", "2026-02-20", "2026-02-15", "2026-03-15")] // before the anchor
    [InlineData("2026-03-15", "2026-02-10", "2026-01-15", "2026-02-15")]
    [InlineData("2025-12-10", "2026-01-09", "2025-12-10", "2026-01-10")] // across the year
    public void Usage_windows_are_monthly_from_the_anchor(string anchor, string at, string start, string end)
    {
        var window = BillingMath.UsageWindow(DateOnly.Parse(anchor), DateOnly.Parse(at));
        Assert.Equal(DateOnly.Parse(start), window.Start);
        Assert.Equal(DateOnly.Parse(end), window.End);
    }

    [Fact]
    public void Previous_usage_window_is_the_one_before_the_current()
    {
        var anchor = DateOnly.Parse("2026-10-09");
        var current = BillingMath.UsageWindow(anchor, DateOnly.Parse("2026-10-20"));
        var previous = BillingMath.UsageWindow(anchor, current.Start.AddDays(-1));
        Assert.Equal(DateOnly.Parse("2026-09-09"), previous.Start);
        Assert.Equal(current.Start, previous.End);
    }

    [Theory]
    [InlineData(1200, 1000, 10, 20.00)]
    [InlineData(1000, 1000, 10, 0)]
    [InlineData(999, 1000, 10, 0)]
    [InlineData(5000, -1, 10, 0)]   // uncapped
    [InlineData(5000, 1000, 0, 0)]  // no overage price: sending stops instead
    public void Overage(int used, int allowance, int centavos, decimal expected)
    {
        Assert.Equal(expected, BillingMath.Overage(used, allowance, centavos));
    }

    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("52998224724", false)]
    [InlineData("111.111.111-11", false)]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11222333000181", true)]
    [InlineData("11222333000180", false)]
    [InlineData("00000000000000", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("123", false)]
    public void Cpf_and_cnpj_check_digits(string? value, bool valid)
    {
        Assert.Equal(valid, BillingMath.IsValidCpfCnpj(value));
    }

    [Fact]
    public void Brasilia_date_lags_utc_by_three_hours()
    {
        Assert.Equal(DateOnly.Parse("2026-10-08"), BillingMath.TodayInBrasilia(Utc(2026, 10, 9, 2)));
        Assert.Equal(DateOnly.Parse("2026-10-09"), BillingMath.TodayInBrasilia(Utc(2026, 10, 9, 3)));
        Assert.Equal(Utc(2026, 10, 9, 3), BillingMath.StartOfDayUtc(DateOnly.Parse("2026-10-09")));
    }

    [Fact]
    public void Cycles_add_a_month_or_a_year()
    {
        Assert.Equal(DateOnly.Parse("2026-11-09"), BillingMath.AddCycle(DateOnly.Parse("2026-10-09"), BillingCycle.Monthly));
        Assert.Equal(DateOnly.Parse("2027-10-09"), BillingMath.AddCycle(DateOnly.Parse("2026-10-09"), BillingCycle.Yearly));
        Assert.Equal("MONTHLY", BillingMath.ToAsaasCycle(BillingCycle.Monthly));
        Assert.Equal("YEARLY", BillingMath.ToAsaasCycle(BillingCycle.Yearly));
    }

    [Fact]
    public void Yearly_price_is_required_for_a_yearly_cycle()
    {
        Assert.Equal(990m, BillingMath.PriceFor(99m, 990m, BillingCycle.Yearly));
        Assert.Equal(99m, BillingMath.PriceFor(99m, null, BillingCycle.Monthly));
        Assert.Throws<InvalidOperationException>(() => BillingMath.PriceFor(99m, null, BillingCycle.Yearly));
    }
}
