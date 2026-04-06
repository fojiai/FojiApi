using Amazon.SQS;
using Amazon.SQS.Model;
using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FojiApi.Infrastructure.Services;

public class FileService(
    FojiDbContext db,
    IStorageService storageService,
    IPlanEnforcementService planEnforcement,
    IAmazonSQS sqsClient,
    IConfiguration configuration,
    ILogger<FileService> logger) : IFileService
{
    private readonly string _sqsQueueUrl = configuration["AWS:SqsFileExtractionQueueUrl"] ?? string.Empty;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",   // .docx
        "application/vnd.openxmlformats-officedocument.presentationml.presentation", // .pptx
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",         // .xlsx
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".pptx", ".xlsx"
    };

    public async Task<FileUploadResult> UploadAsync(int agentId, Stream fileStream, string fileName, long fileSize, string contentType)
    {
        planEnforcement.EnsureFileSizeAllowed(fileSize);

        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            throw new DomainException($"File type '{extension}' is not supported. Allowed: PDF, DOCX, PPTX, XLSX.");

        if (!AllowedContentTypes.Contains(contentType))
            throw new DomainException("Invalid file content type. Allowed: PDF, DOCX, PPTX, XLSX.");

        // Sanitize filename — strip path separators and keep only the file name
        var safeFileName = Path.GetFileName(fileName).Replace("..", "");
        var s3Key = $"agents/{agentId}/{Guid.NewGuid()}/{safeFileName}";
        await storageService.UploadAsync(fileStream, s3Key, contentType);

        var agentFile = new AgentFile
        {
            AgentId = agentId,
            FileName = safeFileName,
            FileSizeBytes = fileSize,
            S3Key = s3Key,
            ContentType = contentType,
            ProcessingStatus = FileProcessingStatus.Pending
        };
        db.AgentFiles.Add(agentFile);
        await db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(_sqsQueueUrl))
        {
            try
            {
                await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = _sqsQueueUrl,
                    MessageBody = JsonSerializer.Serialize(new { job = "extract_file", agentFileId = agentFile.Id })
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enqueue file extraction for AgentFile {AgentFileId}. File was saved but extraction will not start automatically.", agentFile.Id);
            }
        }

        return new FileUploadResult(agentFile.Id, agentFile.FileName, agentFile.FileSizeBytes, agentFile.ProcessingStatus.ToString(), agentFile.CreatedAt);
    }

    public async Task<IEnumerable<FileDetailResult>> GetFilesByAgentAsync(int agentId)
    {
        return await db.AgentFiles
            .Where(f => f.AgentId == agentId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FileDetailResult(
                f.Id, f.FileName, f.FileSizeBytes, f.ContentType,
                f.ProcessingStatus.ToString(), f.ExtractedAt, f.ErrorMessage,
                f.AgentId, f.CreatedAt))
            .ToListAsync();
    }

    public async Task<FileDetailResult> GetFileAsync(int fileId)
    {
        var file = await db.AgentFiles.Include(f => f.Agent).FirstOrDefaultAsync(f => f.Id == fileId)
            ?? throw new NotFoundException("File not found.");

        return new FileDetailResult(
            file.Id, file.FileName, file.FileSizeBytes, file.ContentType,
            file.ProcessingStatus.ToString(), file.ExtractedAt, file.ErrorMessage,
            file.AgentId, file.CreatedAt);
    }

    public async Task<string> GetDownloadUrlAsync(int fileId)
    {
        var file = await db.AgentFiles.FindAsync(fileId)
            ?? throw new NotFoundException("File not found.");

        return await storageService.GetPresignedUrlAsync(file.S3Key, TimeSpan.FromMinutes(15));
    }

    public async Task DeleteFileAsync(int fileId)
    {
        var file = await db.AgentFiles.FindAsync(fileId)
            ?? throw new NotFoundException("File not found.");

        await storageService.DeleteAsync(file.S3Key);
        db.AgentFiles.Remove(file);
        await db.SaveChangesAsync();
    }
}
