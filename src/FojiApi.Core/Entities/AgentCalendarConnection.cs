namespace FojiApi.Core.Entities;

public class AgentCalendarConnection : BaseEntity
{
    public int Id { get; set; }
    public int AgentId { get; set; }

    /// <summary>Google account email that was authorized during OAuth.</summary>
    public string GoogleAccountEmail { get; set; } = string.Empty;

    /// <summary>
    /// AES-256-GCM encrypted Google refresh token.
    /// Format: base64(iv):base64(ciphertext):base64(authTag)
    /// Encrypted by EncryptionService using GOOGLE_CALENDAR_ENCRYPTION_KEY.
    /// </summary>
    public string EncryptedRefreshToken { get; set; } = string.Empty;

    /// <summary>Soft-delete flag. Set to false on disconnect; preserves audit trail.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime ConnectedAt { get; set; }

    // Navigation
    public Agent Agent { get; set; } = null!;
}
