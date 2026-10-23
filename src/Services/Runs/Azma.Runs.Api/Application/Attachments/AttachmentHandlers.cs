using Azma.Auth.ProjectAccess;
using Azma.Common.Pagination;
using Azma.Common.Results;
using Azma.Runs.Api.Application.Common;
using Azma.Runs.Api.Domain;
using Azma.Runs.Api.Infrastructure.Persistence;
using Azma.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Azma.Runs.Api.Application.Attachments;

internal static class AttachmentMapping
{
    public static RunAttachmentDto ToDto(this RunAttachment a) => new(
        a.Id, a.RunId, a.RunCaseId, a.RunResultId,
        a.FileName, a.ContentType, a.SizeBytes,
        a.UploadedByUserId, a.UploadedAtUtc);
}

// ──────────────────────────── Upload ────────────────────────────
public sealed record UploadAttachmentCommand(
    Guid WorkspaceId,
    Guid UserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    Guid? RunCaseId,
    Guid? RunResultId,
    string FileName,
    string ContentType,
    long Size,
    Stream Content) : IRequest<Result<RunAttachmentDto>>;

public sealed class UploadAttachmentHandler : IRequestHandler<UploadAttachmentCommand, Result<RunAttachmentDto>>
{
    private readonly RunsDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly StorageOptions _opt;

    public UploadAttachmentHandler(RunsDbContext db, IObjectStorage storage, IOptions<StorageOptions> opt)
    {
        _db = db;
        _storage = storage;
        _opt = opt.Value;
    }

    public async Task<Result<RunAttachmentDto>> Handle(UploadAttachmentCommand cmd, CancellationToken ct)
    {
        if (cmd.Size <= 0)
            return Result.Failure<RunAttachmentDto>(RunErrors.AttachmentEmpty);
        if (cmd.Size > _opt.MaxUploadBytes)
            return Result.Failure<RunAttachmentDto>(RunErrors.AttachmentTooLarge);

        var run = await _db.Runs
            .AsNoTracking()
            .Where(r => r.Id == cmd.RunId && r.WorkspaceId == cmd.WorkspaceId)
            .Select(r => new { r.Id, r.ProjectId, r.IsDeleted })
            .FirstOrDefaultAsync(ct);
        if (run is null) return Result.Failure<RunAttachmentDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.UserId,
                cmd.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunAttachmentDto>(RunErrors.ProjectAccessDenied);
        if (run.IsDeleted) return Result.Failure<RunAttachmentDto>(RunErrors.RunDeleted);

        if (cmd.RunCaseId is { } rcid)
        {
            var rcExists = await _db.RunCases
                .AsNoTracking()
                .AnyAsync(rc => rc.Id == rcid && rc.RunId == cmd.RunId, ct);
            if (!rcExists) return Result.Failure<RunAttachmentDto>(RunErrors.RunCaseNotFound);
        }

        var attachmentId = Guid.NewGuid();
        var safeName = Path.GetFileName(cmd.FileName);
        var key = $"{cmd.WorkspaceId:N}/{cmd.RunId:N}/{attachmentId:N}_{safeName}";

        await _storage.PutAsync(_opt.DefaultBucket, key, cmd.Content, cmd.Size, cmd.ContentType, ct);

        var att = RunAttachment.Create(
            cmd.WorkspaceId, cmd.RunId, cmd.RunCaseId, cmd.RunResultId,
            safeName, cmd.ContentType, cmd.Size, key, cmd.UserId, attachmentId);

        _db.RunAttachments.Add(att);
        await _db.SaveChangesAsync(ct);
        return att.ToDto();
    }
}

// ──────────────────────────── List ────────────────────────────
public sealed record ListAttachmentsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    Guid? RunCaseId,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<RunAttachmentDto>>>;

public sealed class ListAttachmentsHandler : IRequestHandler<ListAttachmentsQuery, Result<PagedResult<RunAttachmentDto>>>
{
    private readonly RunsDbContext _db;
    public ListAttachmentsHandler(RunsDbContext db) => _db = db;

    public async Task<Result<PagedResult<RunAttachmentDto>>> Handle(ListAttachmentsQuery q, CancellationToken ct)
    {
        var projectId = await _db.Runs.AsNoTracking()
            .Where(r => r.Id == q.RunId && r.WorkspaceId == q.WorkspaceId)
            .Select(r => (Guid?)r.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (projectId is null) return Result.Failure<PagedResult<RunAttachmentDto>>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                projectId.Value,
                ct,
                q.CurrentUserRole))
            return Result.Failure<PagedResult<RunAttachmentDto>>(RunErrors.ProjectAccessDenied);

        var query = _db.RunAttachments
            .AsNoTracking()
            .Where(a => a.WorkspaceId == q.WorkspaceId && a.RunId == q.RunId);
        if (q.RunCaseId is { } rcid) query = query.Where(a => a.RunCaseId == rcid);

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var items = await query
            .OrderByDescending(a => a.UploadedAtUtc)
            .Skip((page - 1) * size).Take(size)
            .Select(a => new RunAttachmentDto(
                a.Id, a.RunId, a.RunCaseId, a.RunResultId,
                a.FileName, a.ContentType, a.SizeBytes,
                a.UploadedByUserId, a.UploadedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<RunAttachmentDto>(items, page, size, total);
    }
}

// ──────────────────────────── Download (presigned URL) ────────────────────────────
public sealed record GetAttachmentUrlQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid AttachmentId) : IRequest<Result<AttachmentUrlDto>>;

public sealed record AttachmentUrlDto(string Url, string FileName, string ContentType, long SizeBytes);

public sealed class GetAttachmentUrlHandler : IRequestHandler<GetAttachmentUrlQuery, Result<AttachmentUrlDto>>
{
    private readonly RunsDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly StorageOptions _opt;

    public GetAttachmentUrlHandler(RunsDbContext db, IObjectStorage storage, IOptions<StorageOptions> opt)
    {
        _db = db;
        _storage = storage;
        _opt = opt.Value;
    }

    public async Task<Result<AttachmentUrlDto>> Handle(GetAttachmentUrlQuery q, CancellationToken ct)
    {
        var att = await _db.RunAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == q.AttachmentId && a.WorkspaceId == q.WorkspaceId, ct);
        if (att is null) return Result.Failure<AttachmentUrlDto>(RunErrors.AttachmentNotFound);
        var projectId = await _db.Runs.AsNoTracking()
            .Where(r => r.Id == att.RunId && r.WorkspaceId == q.WorkspaceId)
            .Select(r => (Guid?)r.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (projectId is null) return Result.Failure<AttachmentUrlDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                projectId.Value,
                ct,
                q.CurrentUserRole))
            return Result.Failure<AttachmentUrlDto>(RunErrors.ProjectAccessDenied);

        var url = await _storage.PresignedDownloadAsync(_opt.DefaultBucket, att.StorageKey, att.FileName, ct);
        return new AttachmentUrlDto(url, att.FileName, att.ContentType, att.SizeBytes);
    }
}

// ──────────────────────────── Delete ────────────────────────────
public sealed record DeleteAttachmentCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid AttachmentId) : IRequest<Result>;

public sealed class DeleteAttachmentHandler : IRequestHandler<DeleteAttachmentCommand, Result>
{
    private readonly RunsDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly StorageOptions _opt;

    public DeleteAttachmentHandler(RunsDbContext db, IObjectStorage storage, IOptions<StorageOptions> opt)
    {
        _db = db;
        _storage = storage;
        _opt = opt.Value;
    }

    public async Task<Result> Handle(DeleteAttachmentCommand cmd, CancellationToken ct)
    {
        var att = await _db.RunAttachments
            .FirstOrDefaultAsync(a => a.Id == cmd.AttachmentId && a.WorkspaceId == cmd.WorkspaceId, ct);
        if (att is null) return Result.Failure(RunErrors.AttachmentNotFound);
        var projectId = await _db.Runs.AsNoTracking()
            .Where(r => r.Id == att.RunId && r.WorkspaceId == cmd.WorkspaceId)
            .Select(r => (Guid?)r.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (projectId is null) return Result.Failure(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                projectId.Value,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure(RunErrors.ProjectAccessDenied);

        _db.RunAttachments.Remove(att);
        await _db.SaveChangesAsync(ct);

        await _storage.DeleteAsync(_opt.DefaultBucket, att.StorageKey, ct);
        return Result.Success();
    }
}
