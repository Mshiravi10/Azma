using Azma.Auth.Extensions;
using Microsoft.EntityFrameworkCore;
using Azma.EventBus.Extensions;
using Azma.Reporting.Api.Endpoints;
using Azma.Reporting.Api.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.WithProperty("Service", "Reporting")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddAzmaAuth(builder.Configuration);
builder.Services.AddAzmaEventBus(builder.Configuration, ReportingServiceCollectionExtensions.RegisterReportingConsumers);
builder.Services.AddReportingModule(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Azma.Reporting", Version = "v1" });
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
builder.Services.AddHealthChecks().AddReportingHealthChecks(builder.Configuration);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Azma.Reporting.Api.Infrastructure.Persistence.ReportingDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Azma.Reporting", status = "ok" }));
app.MapHealthChecks("/health");

app.MapReportingEndpoints();

app.Run();
