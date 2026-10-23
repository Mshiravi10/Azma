using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Application.Users;
using MediatR;

namespace Azma.Identity.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        // Public: serve avatar bytes (so <img src> works without auth headers).
        group.MapGet("/{userId:guid}/avatar",
            async (Guid userId, ISender sender, HttpContext http, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAvatarQuery(userId), ct);
            if (!result.IsSuccess) return Results.NotFound();

            var dto = result.Value!;
            http.Response.Headers.CacheControl = "public, max-age=60";
            return Results.Stream(dto.Content, dto.ContentType);
        }).AllowAnonymous();

        // Authenticated: upload my avatar (multipart form-data, field "file").
        group.MapPost("/me/avatar",
            async (HttpRequest http, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            if (!http.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data with a 'file' field." });

            var form = await http.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "No file uploaded." });

            await using var stream = file.OpenReadStream();
            var result = await sender.Send(
                new UploadAvatarCommand(tenant.RequireUserId(), file.ContentType, file.Length, stream), ct);
            return result.ToHttp();
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        // Authenticated: delete my avatar.
        group.MapDelete("/me/avatar",
            async (ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteAvatarCommand(tenant.RequireUserId()), ct);
            return result.ToHttp();
        }).RequireAuthorization();

        // Admin/dev: backfill UserUpdated events to hydrate downstream read models.
        group.MapPost("/admin/backfill",
            async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new BackfillUsersCommand(), ct);
            return result.ToHttp();
        }).RequireAuthorization();

        return app;
    }
}
