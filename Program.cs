using DotNetEnv;
using DotNetEnv;
using Line.Messaging;
using Microsoft.EntityFrameworkCore;
using NongTimeAI.Data;
using NongTimeAI.Enums;
using NongTimeAI.Services;
using Scalar.AspNetCore;

// โหลด environment variables จาก .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Override configuration with environment variables
builder.Configuration.AddEnvironmentVariables();

// ตรวจสอบ Database Provider ที่เลือก
var databaseProviderString = builder.Configuration["Database:Provider"] 
    ?? Environment.GetEnvironmentVariable("DATABASE_PROVIDER") 
    ?? "PostgreSQL";

if (!Enum.TryParse<DatabaseProvider>(databaseProviderString, true, out var databaseProvider))
{
    throw new InvalidOperationException($"Invalid Database Provider: {databaseProviderString}. Supported providers: PostgreSQL, SqlServer");
}

// สร้าง Connection String ตาม Provider ที่เลือก
var connectionString = GetConnectionString(builder.Configuration, databaseProvider);

// Add DbContext with selected provider
builder.Services.AddDbContext<TimesheetDbContext>(options =>
{
    switch (databaseProvider)
    {
        case DatabaseProvider.PostgreSQL:
            options.UseNpgsql(connectionString);
            break;
        case DatabaseProvider.SqlServer:
            options.UseSqlServer(connectionString);
            break;
        default:
            throw new InvalidOperationException($"Unsupported Database Provider: {databaseProvider}");
    }
});

// Add Controllers
builder.Services.AddControllers();

// Add OpenAPI/Scalar UI
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// Add LINE Messaging API Client
var lineChannelAccessToken = builder.Configuration["Line:ChannelAccessToken"] 
    ?? Environment.GetEnvironmentVariable("LINE_CHANNEL_ACCESS_TOKEN")
    ?? throw new InvalidOperationException("LINE Channel Access Token not configured");

builder.Services.AddSingleton<LineMessagingClient>(_ => 
    new LineMessagingClient(lineChannelAccessToken));

// Register Services
builder.Services.AddScoped<ITimesheetAIService, TimesheetAIService>();
builder.Services.AddScoped<ILineService, LineService>();
builder.Services.AddScoped<ITaskNotificationService, TaskNotificationService>();
builder.Services.AddSingleton<ISessionService, SessionService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("NongTimeAI API")
        .WithTheme(ScalarTheme.Purple)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// เปิดใช้ HTTPS Redirection เฉพาะใน Production
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

app.MapControllers();

// Startup message
app.Logger.LogInformation("🚀 NongTimeAI API Started!");
app.Logger.LogInformation("📊 Database Provider: {Provider}", databaseProvider);
app.Logger.LogInformation("📊 Database Connection: {ConnectionString}", MaskConnectionString(connectionString));
app.Logger.LogInformation("🤖 Ollama: {OllamaUrl}", 
    builder.Configuration["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("OLLAMA_BASE_URL"));
app.Logger.LogInformation("💬 LINE Bot: Configured");

app.Run();

// Helper functions
string GetConnectionString(IConfiguration configuration, DatabaseProvider provider)
{
    return provider switch
    {
        DatabaseProvider.PostgreSQL => configuration.GetConnectionString("PostgreSQL") 
            ?? BuildPostgreSQLConnectionStringFromEnv(),
        DatabaseProvider.SqlServer => configuration.GetConnectionString("SqlServer") 
            ?? BuildSqlServerConnectionStringFromEnv(),
        _ => throw new InvalidOperationException($"Unsupported Database Provider: {provider}")
    };
}

string BuildPostgreSQLConnectionStringFromEnv()
{
    var host = Environment.GetEnvironmentVariable("DATABASE_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("DATABASE_PORT") ?? "5432";
    var database = Environment.GetEnvironmentVariable("DATABASE_NAME") ?? "postgres";
    var username = Environment.GetEnvironmentVariable("DATABASE_USER") ?? "postgres";
    var password = Environment.GetEnvironmentVariable("DATABASE_PASSWORD") ?? "postgres";

    return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
}

string BuildSqlServerConnectionStringFromEnv()
{
    var host = Environment.GetEnvironmentVariable("SQLSERVER_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("SQLSERVER_PORT") ?? "1433";
    var database = Environment.GetEnvironmentVariable("SQLSERVER_DATABASE") ?? "NongTimeAI";
    var username = Environment.GetEnvironmentVariable("SQLSERVER_USER") ?? "sa";
    var password = Environment.GetEnvironmentVariable("SQLSERVER_PASSWORD") ?? "YourPassword123";

    return $"Server={host},{port};Database={database};User Id={username};Password={password};TrustServerCertificate=True;";
}

string MaskConnectionString(string connString)
{
    if (string.IsNullOrEmpty(connString))
        return "Not configured";

    // Mask password
    var parts = connString.Split(';');
    var masked = parts.Select(part =>
    {
        if (part.StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
            return "Password=***";
        return part;
    });

    return string.Join(';', masked);
}
