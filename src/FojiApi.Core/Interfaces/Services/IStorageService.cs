namespace FojiApi.Core.Interfaces.Services;

public interface IStorageService
{
    Task<string> UploadAsync(Stream fileStream, string s3Key, string contentType);
    Task DeleteAsync(string s3Key);
    /// <summary>
    /// Deletes every object under a key prefix. Used when a company is deleted:
    /// its files live under agents/{id}/ and tenant/{companyId}/, and those
    /// objects have no row-level record to delete one by one once the DB rows
    /// are gone. Returns how many objects were removed.
    /// </summary>
    Task<int> DeleteByPrefixAsync(string prefix);
    Task<string> GetPresignedUrlAsync(string s3Key, TimeSpan expiry);
}
