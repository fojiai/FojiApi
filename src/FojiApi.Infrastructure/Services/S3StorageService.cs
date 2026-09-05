using Amazon.S3;
using Amazon.S3.Model;
using FojiApi.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace FojiApi.Infrastructure.Services;

public class S3StorageService(IAmazonS3 s3Client, IConfiguration configuration) : IStorageService
{
    private readonly string _bucketName = configuration["AWS:S3BucketName"]
        ?? throw new InvalidOperationException("AWS:S3BucketName not configured");

    public async Task<string> UploadAsync(Stream fileStream, string s3Key, string contentType)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            InputStream = fileStream,
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };

        await s3Client.PutObjectAsync(request);
        return s3Key;
    }

    public async Task DeleteAsync(string s3Key)
    {
        await s3Client.DeleteObjectAsync(_bucketName, s3Key);
    }

    public async Task<int> DeleteByPrefixAsync(string prefix)
    {
        // Guard against an empty prefix wiping the whole bucket.
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("A non-empty prefix is required.", nameof(prefix));

        var deleted = 0;
        string? continuationToken = null;
        do
        {
            var listed = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken,
            });

            if (listed.S3Objects.Count > 0)
            {
                // DeleteObjects takes up to 1000 keys per call; a page is already
                // capped at 1000, so one delete per listed page is enough.
                await s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = listed.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList(),
                });
                deleted += listed.S3Objects.Count;
            }

            continuationToken = listed.IsTruncated == true ? listed.NextContinuationToken : null;
        }
        while (continuationToken != null);

        return deleted;
    }

    public async Task<string> GetPresignedUrlAsync(string s3Key, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET
        };

        return await s3Client.GetPreSignedURLAsync(request);
    }
}
