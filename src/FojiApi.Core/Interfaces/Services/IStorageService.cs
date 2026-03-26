namespace FojiApi.Core.Interfaces.Services;

public interface IStorageService
{
    Task<string> UploadAsync(Stream fileStream, string s3Key, string contentType);
    Task DeleteAsync(string s3Key);
    Task<string> GetPresignedUrlAsync(string s3Key, TimeSpan expiry);
}
