namespace FojiApi.Core.Interfaces.Services;

public interface IFileService
{
    Task<FileUploadResult> UploadAsync(int agentId, Stream fileStream, string fileName, long fileSize, string contentType);
    Task<IEnumerable<FileDetailResult>> GetFilesByAgentAsync(int agentId);
    Task<FileDetailResult> GetFileAsync(int fileId);
    Task<string> GetDownloadUrlAsync(int fileId);
    Task DeleteFileAsync(int fileId);
}

public record FileUploadResult(int Id, string FileName, long FileSizeBytes, string ProcessingStatus, DateTime CreatedAt);
public record FileDetailResult(int Id, string FileName, long FileSizeBytes, string ContentType, string ProcessingStatus, DateTime? ExtractedAt, string? ErrorMessage, int AgentId, DateTime CreatedAt);
