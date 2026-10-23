namespace Azma.Storage;

public interface IObjectStorage
{
    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);

    Task<StoredObject> PutAsync(
        string bucket,
        string key,
        Stream data,
        long size,
        string contentType,
        CancellationToken ct = default);

    Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default);

    Task DeleteAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>Generate a presigned GET url valid for the configured lifetime.</summary>
    Task<string> PresignedDownloadAsync(
        string bucket,
        string key,
        string? downloadFileName = null,
        CancellationToken ct = default);
}

public sealed record StoredObject(string Bucket, string Key, long Size, string ContentType);
