using System.Text.Json.Serialization;
using Azma.Auth.Extensions;
using Microsoft.EntityFrameworkCore;
using Azma.EventBus.Extensions;
using Azma.Runs.Api.Endpoints;
using Azma.Runs.Api.Extensions;
using Azma.Runs.Api.Infrastructure.Persistence;
using Azma.Storage;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.WithProperty("Service", "Runs")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddAzmaAuth(builder.Configuration);
builder.Services.AddAzmaEventBus(builder.Configuration, RunsServiceCollectionExtensions.RegisterRunsConsumers);
builder.Services.AddRunsModule(builder.Configuration);
builder.Services.AddAzmaStorage(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Azma.Runs", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new()
    {
        { new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});
builder.Services.AddHealthChecks().AddRunsHealthChecks(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// Ensure default bucket exists (best-effort).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var storage = scope.ServiceProvider.GetRequiredService<Azma.Storage.IObjectStorage>();
        var opt = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Azma.Storage.StorageOptions>>().Value;
        await storage.EnsureBucketAsync(opt.DefaultBucket);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to ensure storage bucket on startup");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
    await db.Database.MigrateAsync();
    await ProjectRefSynchronizer.SyncFromProjectsAsync(
        db,
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ProjectRefSynchronizer"));
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Azma.Runs", status = "ok" }));
app.MapHealthChecks("/health");

app.MapRunEndpoints();
app.MapRunScheduleEndpoints();
app.MapAttachmentEndpoints();
app.MapMediaEndpoints();

app.Run();
