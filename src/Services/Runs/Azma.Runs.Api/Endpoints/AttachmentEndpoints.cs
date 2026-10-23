using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Common.Pagination;
using Azma.Runs.Api.Application.Attachments;
using Azma.Runs.Api.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Runs.Api.Endpoints;

public static class AttachmentEndpoints
{
    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Per-run upload + list ──────────────────────────────────
        var runs = app.MapGroup("/api/runs").WithTags("Attachments").RequireAuthorization();

        runs.MapPost("/{runId:guid}/attachments", async (
            Guid runId,
            [FromForm] IFormFile file,
            [FromForm(Name = "runCaseId")] Guid? runCaseId,
            [FromForm(Name = "runResultId")] Guid? runResultId,
            ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "runs.attachment_empty", message = "فایل پیوست خالی است." });

            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadAttachmentCommand(
                tenant.RequireWorkspaceId(),
                tenant.RequireUserId(),
                tenant.AccessScope, tenant.Role ?? string.Empty,
                runId,
                runCaseId,
                runResultId,
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                file.Length,
                stream), ct);
            return result.ToHttp<RunAttachmentDto>(StatusCodes.Status201Created);
        }).DisableAntiforgery();

        runs.MapGet("/{runId:guid}/attachments", async (
            Guid runId, ITenantContext tenant, ISender sender,
            [FromQuery] Guid? runCaseId,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) =>
        {
            var result = await sender.Send(new ListAttachmentsQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                runId, runCaseId, page, pageSize), ct);
            return result.ToHttp<PagedResult<RunAttachmentDto>>();
        });

        // ── Per-attachment download URL + delete ───────────────────
        var atts = app.MapGroup("/api/attachments").WithTags("Attachments").RequireAuthorization();

        atts.MapGet("/{id:guid}/url", async (
            Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAttachmentUrlQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp<AttachmentUrlDto>();
        });

        atts.MapDelete("/{id:guid}", async (
            Guid id, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteAttachmentCommand(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, id), ct);
            return result.ToHttp(StatusCodes.Status204NoContent);
        });

        return app;
    }
}
