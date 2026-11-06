// WealthLedger — Personal finance, clearly tracked.
// Implemented by Computer Engineer Leandro Carlini Mingorance.
// Website: https://lcarlini.github.io/WealthLedger/
// Reach out: https://lcarlini.github.io/lcarlini/

using WealthLedger.Application.Services;
using WealthLedger.Infrastructure.Backup;
using WealthLedger.Infrastructure.IoC;
using WealthLedger.WebApp.Backup;
using Scalar.AspNetCore;
using WealthLedger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Machine-specific overrides (gitignored). Prefer this over committing personal paths/tokens.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var sqliteConnectionString = ResolveSqliteConnectionString(builder.Configuration);
var databaseLocation = new DatabaseLocation(GetDatabasePath(sqliteConnectionString));
builder.Services.AddSingleton(databaseLocation);
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddDbContext<WealthLedgerDbContext>(options =>
    options.UseSqlite(sqliteConnectionString));

builder.Services.AddHttpClient();
builder.Services.ConfigureServices();
builder.Services.AddHostedService<BackupSchedulerService>();
builder.Services.AddHostedService<WealthLedger.WebApp.Jobs.DailySnapshotService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Only the local SPA origins may call the API from a browser (the built SPA is same-origin anyway).
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:5000", "https://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WealthLedger.Database");
logger.LogInformation("Using SQLite database at {DatabasePath}", databaseLocation.DatabasePath);

// Order matters: a staged restore replaces the file, a pre-upgrade backup protects the schema change,
// and only then does EF touch the database.
var backups = app.Services.GetRequiredService<IBackupService>();
await backups.ApplyPendingRestoreAsync();
await backups.CreatePreUpgradeBackupAsync();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WealthLedgerDbContext>();
    db.Database.EnsureCreated();
    await SqliteSchemaMigrator.ApplyAsync(db, logger);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.MapControllers();

// SPA caching: never cache index.html (so deploys show up without Ctrl+F5).
// Hashed Angular bundles (main-XXXX.js / styles-XXXX.css) can be cached forever.
var spaStaticOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx => ApplySpaCacheHeaders(ctx.Context, ctx.File.Name),
};

app.UseStaticFiles(spaStaticOptions);
app.MapFallbackToFile("/index.html", spaStaticOptions);

app.Run();

static void ApplySpaCacheHeaders(HttpContext context, string fileName)
{
    var headers = context.Response.Headers;

    if (fileName.Equals("index.html", StringComparison.OrdinalIgnoreCase))
    {
        headers.CacheControl = "no-cache, no-store, must-revalidate, max-age=0";
        headers["Pragma"] = "no-cache";
        headers.Expires = "0";
        headers["Surrogate-Control"] = "no-store";
        return;
    }

    // Angular production fingerprints: main-ABCDEF12.js, styles-XYZ.css, chunk-....js
    if (IsFingerprintedAsset(fileName))
    {
        headers.CacheControl = "public,max-age=31536000,immutable";
        return;
    }

    // logo.svg, favicon, unhashed assets — revalidate so republish is visible
    headers.CacheControl = "no-cache";
}

static bool IsFingerprintedAsset(string fileName)
{
    // e.g. main-CTGS726Z.js, styles-SFU6RCFC.css, chunk-QBS7N3O6.js, worker-B377NUQQ.js
    var name = Path.GetFileNameWithoutExtension(fileName);
    var ext = Path.GetExtension(fileName);
    if (ext is not (".js" or ".css" or ".woff" or ".woff2"))
        return false;

    var dash = name.LastIndexOf('-');
    if (dash < 0 || dash >= name.Length - 1)
        return false;

    var suffix = name[(dash + 1)..];
    return suffix.Length >= 6 && suffix.All(c => char.IsLetterOrDigit(c));
}

static string ResolveSqliteConnectionString(IConfiguration configuration)
{
    const string defaultFileName = "wealthledger.db";

    var configured = configuration.GetConnectionString("Default");
    var dataSource = configured is not null &&
                     configured.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
        ? configured["Data Source=".Length..].Trim().Trim('"')
        : defaultFileName;

    // Absolute Data Source paths are used as-is (advanced override).
    if (Path.IsPathRooted(dataSource))
    {
        EnsureParentDirectory(dataSource);
        return $"Data Source={dataSource}";
    }

    // Relative DB file names resolve under (in order):
    // 1) WEALTHLEDGER_DATA_DIR env var
    // 2) WealthLedger:DataDirectory from appsettings / appsettings.Local.json
    // 3) folder next to the deployed binaries
    var dataDirectory =
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("WEALTHLEDGER_DATA_DIR"),
            configuration["WealthLedger:DataDirectory"])
        ?? AppContext.BaseDirectory;

    dataDirectory = Environment.ExpandEnvironmentVariables(dataDirectory.Trim().Trim('"'));
    Directory.CreateDirectory(dataDirectory);

    dataSource = Path.Combine(dataDirectory, dataSource);
    return $"Data Source={dataSource}";
}

static string? FirstNonEmpty(params string?[] values)
{
    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return value;
    }

    return null;
}

static void EnsureParentDirectory(string filePath)
{
    var directory = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrWhiteSpace(directory))
        Directory.CreateDirectory(directory);
}

static string GetDatabasePath(string connectionString)
{
    const string prefix = "Data Source=";
    return connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? connectionString[prefix.Length..].Trim()
        : connectionString;
}
