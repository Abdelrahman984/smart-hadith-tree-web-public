using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Etl.Parsers;
using SmartHadithTree.Etl.Services;
using SmartHadithTree.Infrastructure.Data;

// ── Build the Host ─────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);

// Load configuration from appsettings.json
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    // Last, so that ConnectionStrings__DefaultConnection overrides the file (a scratch database for a test load).
    .AddEnvironmentVariables();

// ── Register Services ──────────────────────────────────────────────
builder.Services.AddDbContext<HadithTreeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.CommandTimeout(600))); // 10 minutes for bulk operations

// Parsers (pluggable — add new parsers here)
builder.Services.AddTransient<IDataSourceParser, SmartHadithTree.Etl.Parsers.Shamela.ShamelaDatasetParser>();
builder.Services.AddTransient<IDataSourceParser, SmartHadithTree.Etl.Parsers.Shamela.ShamelaJsonParser>();
builder.Services.AddTransient<IDataSourceParser, SeedDataGenerator>();

// Services
builder.Services.AddTransient<BulkDataIngestionService>();
builder.Services.AddTransient<ShamelaIlalService>();
builder.Services.AddTransient<EtlOrchestrator>();

var host = builder.Build();

// ── Run the ETL Pipeline ───────────────────────────────────────────
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var orchestrator = scope.ServiceProvider.GetRequiredService<EtlOrchestrator>();

// Determine the data source from command-line args or default to "seed"
var source = args.Length > 0 ? args[0] : "seed";

logger.LogInformation("╔══════════════════════════════════════════════╗");
logger.LogInformation("║   Smart Hadith Tree — ETL Data Pipeline     ║");
logger.LogInformation("║   شجرة الأسانيد الذكية — خط أنابيب البيانات ║");
logger.LogInformation("╚══════════════════════════════════════════════╝");
logger.LogInformation("Source: {Source}", source);
var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("DefaultConnection"));
logger.LogInformation("Database: {Server} / {Database}", target.DataSource, target.InitialCatalog);

try
{
    if (source.Equals("seed-ilal-shamela", StringComparison.OrdinalIgnoreCase))
    {
        // Mudallisin and mukhtalitun of the Shamela books, onto narrators loaded from data/shamela_rijal.
        // E.g. dotnet run -- seed-ilal-shamela data/shamela_rijal
        var rijalPath = args.Length > 1 ? args[1] : "data/shamela_rijal";
        var shamelaIlal = scope.ServiceProvider.GetRequiredService<ShamelaIlalService>();
        await shamelaIlal.RunAsync(Path.Combine(rijalPath, "ilal.json"), CancellationToken.None);
    }
    else
    {
        await orchestrator.RunAsync(source, CancellationToken.None);
    }
    
    logger.LogInformation("ETL completed successfully. ✓");
    return 0;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "ETL pipeline failed with a critical error.");
    return 1;
}
