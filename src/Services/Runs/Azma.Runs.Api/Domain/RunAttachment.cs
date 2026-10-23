namespace Azma.Runs.Api.Domain;

public sealed class RunAttachment
{
    private RunAttachment() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid RunId { get; private set; }

    /// <summary>Optional link to a specific run case.</summary>
    public Guid? RunCaseId { get; private set; }

    /// <summary>Optional link to a specific result snapshot.</summary>
    public Guid? RunResultId { get; private set; }

    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }

    /// <summary>Object key inside the storage bucket.</summary>
    public string StorageKey { get; private set; } = default!;

    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; } = DateTime.UtcNow;

    public static RunAttachment Create(
        Guid workspaceId, Guid runId, Guid? runCaseId, Guid? runResultId,
        string fileName, string contentType, long size, string storageKey, Guid byUserId,
        Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        WorkspaceId = workspaceId,
        RunId = runId,
        RunCaseId = runCaseId,
        RunResultId = runResultId,
        FileName = fileName,
        ContentType = contentType,
        SizeBytes = size,
        StorageKey = storageKey,
        UploadedByUserId = byUserId,
    };
}
