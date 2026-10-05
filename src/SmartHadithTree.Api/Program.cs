using SmartHadithTree.Api;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Infrastructure.Data;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Infrastructure.Data.Repositories;
using Microsoft.SemanticKernel;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ── Database ───────────────────────────────────────────────────────
builder.Services.AddDbContext<HadithTreeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(120);
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
        }));

// Register Semantic Kernel
// Provider order: Together AI (OpenAI-compatible endpoint) if Together:ApiKey is set, else Gemini, else no model.
var togetherApiKey = builder.Configuration["Together:ApiKey"];
var geminiApiKey = builder.Configuration["Gemini:ApiKey"];
if (!string.IsNullOrEmpty(togetherApiKey))
{
    var togetherModel = builder.Configuration["Together:Model"] ?? "zai-org/GLM-5.3-Flash";
    var togetherEndpoint = new Uri(builder.Configuration["Together:Endpoint"] ?? "https://api.together.xyz/v1");
    // Together:ExtraBody (JSON object) adds provider-specific request fields, e.g. to switch the model's "thinking" off.
    var togetherExtraRaw = builder.Configuration["Together:ExtraBody"];
    var togetherExtra = ExtraBodyHandler.Parse(togetherExtraRaw);
    if (!string.IsNullOrWhiteSpace(togetherExtraRaw))
        Console.WriteLine(togetherExtra is null
            ? $"WARNING: Together:ExtraBody is not a valid JSON object and is ignored. Value: {togetherExtraRaw}"
            : $"Together:ExtraBody applied: {togetherExtra.ToJsonString()}");
    var togetherHttp = togetherExtra is null
        ? null
        : new HttpClient(new ExtraBodyHandler(togetherExtra) { InnerHandler = new HttpClientHandler() });
    builder.Services.AddKernel().AddOpenAIChatCompletion(togetherModel, togetherEndpoint, togetherApiKey, httpClient: togetherHttp);
}
else if (!string.IsNullOrEmpty(geminiApiKey) && geminiApiKey != "YOUR_API_KEY_HERE")
{
    builder.Services.AddKernel().AddGoogleAIGeminiChatCompletion("gemini-1.5-pro", geminiApiKey);
}
else
{
    // Dummy kernel if no API key is provided
    builder.Services.AddKernel();
}

builder.Services.AddScoped<IAiEvaluationService, AiEvaluationService>();
builder.Services.AddScoped<IIlalExplanationService, IlalExplanationService>();
builder.Services.AddScoped<IHadithVerificationService, HadithVerificationService>();
builder.Services.AddScoped<ISearchJudgeService, SearchJudgeService>();

// Register Services
builder.Services.AddScoped<IHadithTreeDbContext>(provider => provider.GetRequiredService<HadithTreeDbContext>());
builder.Services.AddScoped<IHadithChainRepository, HadithChainRepository>();
builder.Services.AddScoped<IHadithSearchService, HadithSearchService>();
builder.Services.AddScoped<INarratorService, NarratorService>();
builder.Services.AddScoped<IBooksService, BooksService>();
builder.Services.AddScoped<ITaqwiyahService, TaqwiyahService>();
builder.Services.AddScoped<IIlalAnalysisService, IlalAnalysisService>();
builder.Services.AddScoped<IGawamiImporterService, SmartHadithTree.Infrastructure.Data.Gawami.GawamiImporterService>();

// ── Controllers ────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Prevent Unicode-escaping Arabic characters in JSON responses.
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ── Reverse proxy ──────────────────────────────────────────────────
// Behind Caddy/nginx every request reaches the API from the proxy's address. Reading X-Forwarded-For/-Proto gives
// the real client address (the rate limiter below is per client) and the original scheme. Only the last hop is
// trusted (ForwardLimit 1), so a client cannot choose its own address by sending the header itself, provided the
// API is reachable only through the proxy (docker-compose.prod.yml binds it to localhost). Set
// ForwardedHeaders__Enabled=false when the API is exposed directly.
var forwardedHeadersEnabled = builder.Configuration.GetValue("ForwardedHeaders:Enabled", true);
if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// ── Rate limiting ──────────────────────────────────────────────────
// The AI endpoints call a paid model without a login, so each client address gets a small budget per minute
// (RateLimit:AiPermitPerMinute, default 10).
var aiPermitPerMinute = builder.Configuration.GetValue("RateLimit:AiPermitPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = aiPermitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// ── CORS ───────────────────────────────────────────────────────────
// Allowed browser origins come from configuration (Cors:AllowedOrigins, or the environment
// variables Cors__AllowedOrigins__0, Cors__AllowedOrigins__1, ...). Defaults to the Next.js dev server.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:3000", "https://localhost:3000" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ── Swagger (dev only) ─────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Smart Hadith Tree API — شجرة الأسانيد الذكية",
        Version = "v1",
        Description = "RESTful API for Hadith Isnad tree visualization and AI-powered narrator evaluation."
    });
});

var app = builder.Build();

// ── Apply Pending Migrations ───────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<HadithTreeDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration check failed at startup. Ensure the 'MSSQLSERVER' Windows service is running.");
    }
}

// ── Middleware Pipeline ────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
}

// Behind a TLS-terminating proxy (container platforms) the app only sees plain HTTP.
if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.MapControllers();

app.Run();
