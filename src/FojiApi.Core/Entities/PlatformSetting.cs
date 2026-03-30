namespace FojiApi.Core.Entities;

public class PlatformSetting : BaseEntity
{
    public int Id { get; set; }

    /// <summary>Unique key, e.g. "OPENAI_API_KEY", "GEMINI_API_KEY", "AWS_ACCESS_KEY_ID".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The stored value (encrypted at rest via column-level encryption is recommended).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Whether this setting contains a secret (masks display in UI).</summary>
    public bool IsSecret { get; set; } = true;

    /// <summary>Human-readable label for the UI, e.g. "OpenAI API Key".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional grouping, e.g. "openai", "gemini", "bedrock".</summary>
    public string Category { get; set; } = string.Empty;
}
