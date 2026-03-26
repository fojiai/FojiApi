using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

public class AgentFile : BaseEntity
{
    public int Id { get; set; }
    public int AgentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    // S3 key for the original uploaded file
    public string S3Key { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    // Extraction status
    public FileProcessingStatus ProcessingStatus { get; set; } = FileProcessingStatus.Pending;
    public int ExtractionVersion { get; set; } = 0;
    public DateTime? ExtractedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // S3 keys for extracted artifacts
    // Path pattern: tenant/{companyId}/files/{fileId}/extractions/{version}/
    public string? S3RawTextKey { get; set; }
    public string? S3NormalizedTextKey { get; set; }
    public string? S3ChunksKey { get; set; }

    // Navigation
    public Agent Agent { get; set; } = null!;
}
