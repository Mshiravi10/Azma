using System.Text.Json.Serialization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.WithProperty("Service", "Automation")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddScoped<ScenarioRecordingRunner>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Azma.Automation", Version = "v1" });
});
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();

app.MapGet("/", () => Results.Ok(new { service = "Azma.Automation", status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapPost("/api/automation/validate", (AutomationValidationRequest request) =>
{
    var validation = RecordedScenarioValidator.Validate(request.Definition);
    return Results.Ok(validation);
});

app.MapPost("/api/automation/run", async (
    AutomationRunRequest request,
    ScenarioRecordingRunner runner,
    CancellationToken ct) =>
{
    var validation = RecordedScenarioValidator.Validate(request.Definition);
    if (!validation.IsValid || validation.SanitizedDefinition is null)
    {
        return Results.Ok(new AutomationRunResponse(
            false,
            AutomationFailureKind.ValidationFailure,
            string.Join("; ", validation.Errors),
            0,
            Array.Empty<AutomationStepResult>(),
            Array.Empty<AutomationNetworkResult>()));
    }

    var sanitized = request with { Definition = validation.SanitizedDefinition };
    var result = await runner.RunAsync(sanitized, ct);
    return Results.Ok(result);
});

app.Run();
