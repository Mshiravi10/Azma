using Azma.Auth.Extensions;
using Azma.Gateway.Mcp;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.WithProperty("Service", "Gateway")
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddAzmaAuth(builder.Configuration);
builder.Services.Configure<McpOptions>(builder.Configuration.GetSection(McpOptions.SectionName));

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var mcpEnabled = builder.Configuration.GetValue("Mcp:Enabled", builder.Environment.IsDevelopment());
if (mcpEnabled)
{
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpClient(AzmaMcpConstants.ApiHttpClientName, ConfigureMcpHttpClient);
    builder.Services.AddHttpClient(AzmaMcpConstants.AuthHttpClientName, ConfigureMcpHttpClient);
    builder.Services.AddSingleton<IMcpTokenValidator, McpTokenValidator>();
    builder.Services.AddSingleton<IAzmaTokenProvider, McpRequestTokenProvider>();
    builder.Services.AddSingleton<AzmaApiClient>();
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options => options.Stateless = true)
        .WithToolsFromAssembly();
}

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Azma.Gateway", status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

if (mcpEnabled && app.Environment.IsDevelopment())
{
    app.UseAzmaMcpAccessGuard();
    app.UseAzmaMcpAuditLogging();
    app.UseAzmaMcpTokenAuthentication();
    app.MapMcp("/mcp");
}

app.MapReverseProxy();

app.Run();

static void ConfigureMcpHttpClient(IServiceProvider services, HttpClient client)
{
    var options = services.GetRequiredService<IOptionsMonitor<McpOptions>>().CurrentValue;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.HttpTimeoutSeconds, 1, 60));
}
