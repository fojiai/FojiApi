namespace FojiApi.Core.Entities;

/// <summary>
/// A long-lived credential the mobile app trades for fresh 24h access tokens,
/// so people aren't logged out of their phone every day.
///
/// Only a SHA-256 hash is stored — the raw token exists on the device and in the
/// one response that issued it. Tokens rotate on every use: the old one is
/// revoked and points at its replacement. Presenting a revoked token again means
/// it was copied, so every session of that user is revoked.
/// </summary>
public class RefreshToken : BaseEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>Hex SHA-256 of the raw token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash of the token this one was rotated into, if any.</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Optional label from the app ("iPhone de Ana") for a future sessions list.</summary>
    public string? DeviceName { get; set; }

    public User User { get; set; } = null!;
}
