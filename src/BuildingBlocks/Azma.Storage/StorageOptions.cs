namespace Azma.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "azma";
    public string SecretKey { get; set; } = "azma_dev_password";
    public bool UseSsl { get; set; } = false;
    public string? Region { get; set; }

    /// <summary>Default bucket created on startup if missing.</summary>
    public string DefaultBucket { get; set; } = "azma";

    /// <summary>Lifetime in seconds for presigned download URLs.</summary>
    public int PresignedUrlSeconds { get; set; } = 3600;

    /// <summary>Maximum upload size in bytes (default 25MB).</summary>
    public long MaxUploadBytes { get; set; } = 25L * 1024 * 1024;
}
