// WealthLedger — Personal finance, clearly tracked.
// Implemented by Computer Engineer Leandro Carlini Mingorance.
// Website: https://lcarlini.github.io/WealthLedger/
// Reach out: https://lcarlini.github.io/lcarlini/

using WealthLedger.Infrastructure.IoC;
using Scalar.AspNetCore;
using WealthLedger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Machine-specific overrides (gitignored). Prefer this over committing personal paths/tokens.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var sqliteConnectionString = ResolveSqliteConnectionString(builder.Configuration);
builder.Services.AddDbContext<WealthLedgerDbContext>(options =>
    options.UseSqlite(sqliteConnectionString));

builder.Services.AddHttpClient();
builder.Services.ConfigureServices();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WealthLedger.Database");
logger.LogInformation("Using SQLite database at {DatabasePath}", GetDatabasePath(sqliteConnectionString));

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
app.UseStaticFiles();
app.MapFallbackToFile("/index.html");

app.Run();

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
