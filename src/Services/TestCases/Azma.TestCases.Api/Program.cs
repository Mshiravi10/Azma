using System.Text.Json.Serialization;
using Azma.Auth.Extensions;
using Microsoft.EntityFrameworkCore;
using Azma.EventBus.Extensions;
using Azma.TestCases.Api.Endpoints;
using Azma.TestCases.Api.Extensions;
using Azma.TestCases.Api.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.WithProperty("Service", "TestCases")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddAzmaAuth(builder.Configuration);
builder.Services.AddAzmaEventBus(builder.Configuration, TestCasesServiceCollectionExtensions.RegisterTestCasesConsumers);
builder.Services.AddTestCasesModule(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Azma.TestCases", Version = "v1" });
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
builder.Services.AddHealthChecks().AddTestCasesHealthChecks(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Azma.TestCases.Api.Infrastructure.Persistence.TestCasesDbContext>();
    await db.Database.MigrateAsync();
    await ProjectRefSynchronizer.SyncFromProjectsAsync(
        db,
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ProjectRefSynchronizer"));
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Azma.TestCases", status = "ok" }));
app.MapHealthChecks("/health");

app.MapTestCaseEndpoints();
app.MapCustomFieldEndpoints();

app.Run();
