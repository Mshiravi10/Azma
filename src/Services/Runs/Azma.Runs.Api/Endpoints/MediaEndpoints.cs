using Azma.Auth.Tenant;
using Azma.Storage;
using Microsoft.Extensions.Options;

namespace Azma.Runs.Api.Endpoints;

public static class MediaEndpoints
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml",
    };

    private static readonly Dictionary<string, string> ExtToMime = new(StringComparer.OrdinalIgnoreCase)
    {
        { "jpg",  "image/jpeg"      },
        { "jpeg", "image/jpeg"      },
        { "png",  "image/png"       },
        { "gif",  "image/gif"       },
        { "webp", "image/webp"      },
        { "svg",  "image/svg+xml"   },
    };

    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Upload (requires auth) ────────────────────────────────────────
        app.MapPost("/api/media/images", async (
            HttpRequest request,
            ITenantContext tenant,
            IObjectStorage storage,
            IOptions<StorageOptions> opt,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { message = "فرمت درخواست باید multipart/form-data باشد." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
                return Results.BadRequest(new { message = "فایلی ارسال نشده است." });

            var mime = (file.ContentType ?? "").ToLowerInvariant();
            if (!AllowedMimeTypes.Contains(mime))
                return Results.BadRequest(new { message = "فقط تصاویر مجاز هستند (JPEG، PNG، GIF، WebP، SVG)." });

            if (file.Length > MaxBytes)
                return Results.BadRequest(new { message = "حجم تصویر نباید بیشتر از ۱۰ مگابایت باشد." });

            var workspaceId = tenant.RequireWorkspaceId();
            var imageId     = Guid.NewGuid();
            var ext = mime switch
            {
                "image/jpeg"    => "jpg",
                "image/png"     => "png",
                "image/gif"     => "gif",
                "image/webp"    => "webp",
                "image/svg+xml" => "svg",
                _               => "bin",
            };

            var key = $"inline-images/{workspaceId}/{imageId}.{ext}";

            await using var stream = file.OpenReadStream();
            await storage.PutAsync(opt.Value.DefaultBucket, key, stream, file.Length, mime, ct);

            var url = $"/api/media/images/{workspaceId}/{imageId}.{ext}";
            return Results.Ok(new { id = imageId, url });
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithTags("Media");

        // ── Serve (no auth – required so <img src="..."> works in the browser) ──
        app.MapGet("/api/media/images/{workspaceId:guid}/{filename}", async (
            Guid workspaceId,
            string filename,
            IObjectStorage storage,
            IOptions<StorageOptions> opt,
            HttpResponse response,
            CancellationToken ct) =>
        {
            // Allow only safe filenames: {guid}.{ext}
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    filename, @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.(jpg|jpeg|png|gif|webp|svg)$"))
            {
                return Results.NotFound();
            }

            var key = $"inline-images/{workspaceId}/{filename}";
            var rawExt = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
            var contentType = ExtToMime.GetValueOrDefault(rawExt, "application/octet-stream");

            try
            {
                var stream = await storage.GetAsync(opt.Value.DefaultBucket, key, ct);
                response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return Results.Stream(stream, contentType);
            }
            catch
            {
                return Results.NotFound();
            }
        })
        .AllowAnonymous()
        .WithTags("Media");

        return app;
    }
}
