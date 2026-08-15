namespace FojiApi.Core.Utilities;

/// <summary>
/// Normalises incoming DateTimes to UTC before they reach Postgres.
///
/// Every timestamp column is "timestamp with time zone", and Npgsql refuses to
/// write a DateTime whose Kind is not Utc — it throws rather than guessing a
/// zone. JSON bodies routinely arrive as date-only ("2026-08-15", from an
/// input[type=date]) which deserializes with Kind=Unspecified and would
/// otherwise blow up at SaveChanges with a 500.
/// </summary>
public static class DateTimeUtc
{
    /// <summary>
    /// Coerce to UTC. Unspecified is treated as already-UTC rather than local,
    /// so a plain calendar date lands on that day instead of shifting by the
    /// server's offset.
    /// </summary>
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static DateTime? Normalize(DateTime? value) =>
        value.HasValue ? Normalize(value.Value) : null;
}
