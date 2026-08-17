using System.Security.Cryptography;
using System.Text;
using FojiApi.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace FojiApi.Infrastructure.Services;

/// <summary>
/// AES-256-GCM symmetric encryption for storing sensitive secrets (e.g. Google OAuth refresh tokens).
/// Output format: base64(12-byte IV):base64(ciphertext):base64(16-byte auth tag)
/// Key source: Encryption:Key (base64-encoded 32 bytes), falling back to the
/// legacy GoogleCalendar:EncryptionKey. The name predates this being used for
/// anything but calendar tokens — it now also protects per-agent WhatsApp
/// tokens, so a tenant who never touched Google Calendar still needs it.
/// The same key value must be set in foji-ai-api and foji-worker.
/// </summary>
public class EncryptionService(IConfiguration configuration) : IEncryptionService
{
    private byte[] GetKey()
    {
        var keyB64 = configuration["Encryption:Key"]
            ?? configuration["GoogleCalendar:EncryptionKey"]
            ?? throw new InvalidOperationException(
                "No encryption key configured. Set Encryption:Key (or the legacy GoogleCalendar:EncryptionKey) to a base64-encoded 32-byte value.");
        var key = Convert.FromBase64String(keyB64);
        if (key.Length != 32)
            throw new InvalidOperationException("The encryption key must be exactly 32 bytes (base64-encoded).");
        return key;
    }

    public string Encrypt(string plaintext)
    {
        var key = GetKey();
        var iv = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(iv, plaintextBytes, ciphertext, tag);

        return $"{Convert.ToBase64String(iv)}:{Convert.ToBase64String(ciphertext)}:{Convert.ToBase64String(tag)}";
    }

    public string Decrypt(string encryptedValue)
    {
        var key = GetKey();
        var parts = encryptedValue.Split(':');
        if (parts.Length != 3)
            throw new FormatException("Invalid encrypted value format. Expected: base64(iv):base64(ciphertext):base64(tag)");

        var iv = Convert.FromBase64String(parts[0]);
        var ciphertext = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(iv, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
