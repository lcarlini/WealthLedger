// WealthLedger — Personal finance, clearly tracked.
// Implemented by Computer Engineer Leandro Carlini Mingorance.
// Website: https://lcarlini.github.io/WealthLedger/
// Reach out: https://lcarlini.github.io/lcarlini/

using WealthLedger.Infrastructure.IoC;
using Scalar.AspNetCore;
using WealthLedger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Always resolve the SQLite file next to the deployed binaries (not the process cwd).
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
        ? configured["Data Source=".Length..].Trim()
        : defaultFileName;

    if (!Path.IsPathRooted(dataSource))
        dataSource = Path.Combine(AppContext.BaseDirectory, dataSource);

    return $"Data Source={dataSource}";
}

static string GetDatabasePath(string connectionString)
{
    const string prefix = "Data Source=";
    return connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? connectionString[prefix.Length..].Trim()
        : connectionString;
}
