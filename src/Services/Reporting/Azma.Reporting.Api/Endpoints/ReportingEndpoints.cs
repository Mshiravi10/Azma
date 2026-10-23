using Azma.Auth.Tenant;
using Azma.Common.Http;
using Azma.Reporting.Api.Application.Common;
using Azma.Reporting.Api.Application.Reporting;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Azma.Reporting.Api.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var dash = app.MapGroup("/api/dashboards").WithTags("Dashboards").RequireAuthorization();

        dash.MapGet("/workspace", async (ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetWorkspaceDashboardQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty), ct);
            return result.ToHttp<WorkspaceDashboardDto>();
        });

        dash.MapGet("/project/{projectId:guid}", async (
            Guid projectId, ITenantContext tenant, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetProjectDashboardQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, projectId), ct);
            return result.ToHttp<ProjectDashboardDto>();
        });

        var rep = app.MapGroup("/api/reporting").WithTags("Reporting").RequireAuthorization();

        rep.MapGet("/trends", async (
            ITenantContext tenant, ISender sender,
            [FromQuery] Guid? projectId,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to,
            [FromQuery] string granularity = "day",
            CancellationToken ct = default) =>
        {
            var toUtc = to ?? DateTime.UtcNow;
            var fromUtc = from ?? toUtc.AddDays(-30);
            var result = await sender.Send(new GetTrendsQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty, projectId,
                DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(toUtc, DateTimeKind.Utc),
                granularity), ct);
            return result.ToHttp<IReadOnlyList<TrendBucketDto>>();
        });

        rep.MapGet("/top-failures", async (
            ITenantContext tenant, ISender sender,
            [FromQuery] Guid? projectId,
            [FromQuery] int days = 30,
            [FromQuery] int limit = 10,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new GetTopFailingTestsQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                projectId, days, limit), ct);
            return result.ToHttp<IReadOnlyList<TopFailingTestDto>>();
        });

        rep.MapGet("/users", async (
            ITenantContext tenant, ISender sender,
            [FromQuery] Guid? projectId,
            [FromQuery] int days = 30,
            [FromQuery] int limit = 25,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new GetUserActivityQuery(
                tenant.RequireWorkspaceId(), tenant.RequireUserId(), tenant.AccessScope, tenant.Role ?? string.Empty,
                projectId, days, limit), ct);
            return result.ToHttp<IReadOnlyList<UserActivityDto>>();
        });

        return app;
    }
}
