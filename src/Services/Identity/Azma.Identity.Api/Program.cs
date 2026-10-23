using Azma.Auth.Extensions;
using Azma.EventBus.Extensions;
using Azma.Identity.Api.Endpoints;
using Azma.Identity.Api.Extensions;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Storage;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.WithProperty("Service", "Identity")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddAzmaAuth(builder.Configuration);
builder.Services.AddAzmaEventBus(builder.Configuration, Azma.Identity.Api.Extensions.IdentityServiceCollectionExtensions.RegisterIdentityConsumers);
builder.Services.AddAzmaStorage(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Azma.Identity", Version = "v1" });
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
builder.Services.AddHealthChecks().AddIdentityHealthChecks(builder.Configuration);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

// Ensure avatars bucket exists (best-effort).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var opt = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value;
        await storage.EnsureBucketAsync(opt.DefaultBucket);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to ensure storage bucket on startup");
    }
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Azma.Identity", status = "ok" }));
app.MapHealthChecks("/health");

app.MapAuthEndpoints();
app.MapMemberEndpoints();
app.MapUserEndpoints();

app.Run();
