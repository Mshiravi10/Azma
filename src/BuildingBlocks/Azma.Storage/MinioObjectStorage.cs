using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Azma.Storage;

public sealed class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _client;
    private readonly StorageOptions _opt;
    private readonly ILogger<MinioObjectStorage> _log;

    public MinioObjectStorage(
        IMinioClient client,
        IOptions<StorageOptions> options,
        ILogger<MinioObjectStorage> log)
    {
        _client = client;
        _opt = options.Value;
        _log = log;
    }

    public async Task EnsureBucketAsync(string bucket, CancellationToken ct = default)
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
            _log.LogInformation("Created bucket {Bucket}", bucket);
        }
    }

    public async Task<StoredObject> PutAsync(
        string bucket, string key, Stream data, long size, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(data)
            .WithObjectSize(size)
            .WithContentType(contentType), ct);
        return new StoredObject(bucket, key, size, contentType);
    }

    public async Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithCallbackStream(s => s.CopyTo(ms)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken ct = default)
    {
        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(key), ct);
        }
        catch (ObjectNotFoundException)
        {
            // best-effort delete
        }
    }

    public async Task<string> PresignedDownloadAsync(
        string bucket, string key, string? downloadFileName = null, CancellationToken ct = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithExpiry(_opt.PresignedUrlSeconds);

        if (!string.IsNullOrWhiteSpace(downloadFileName))
        {
            args = args.WithHeaders(new Dictionary<string, string>
            {
                ["response-content-disposition"] = $"attachment; filename=\"{downloadFileName}\"",
            });
        }

        return await _client.PresignedGetObjectAsync(args);
    }
}
