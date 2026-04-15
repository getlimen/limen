using Limen.API.Endpoints;
using Limen.API.Middleware;
using Limen.Application.Common;
using Limen.Infrastructure;
using Limen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

#region Configure Services
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "oidc";
})
.AddCookie()
.AddOpenIdConnect("oidc", options =>
{
    var cfg = builder.Configuration.GetSection("Oidc");
    options.Authority = cfg["Authority"];
    options.ClientId = cfg["ClientId"];
    options.ClientSecret = cfg["ClientSecret"];
    options.CallbackPath = cfg["CallbackPath"] ?? "/auth/oidc/callback";
    options.ResponseType = "code";
    options.UsePkce = true;
    options.SaveTokens = true;
    foreach (var s in cfg.GetSection("Scopes").Get<string[]>() ?? Array.Empty<string>())
    {
        options.Scope.Add(s);
    }
});
builder.Services.AddAuthorization();
#endregion

var app = builder.Build();

#region Database Initialization with Retry Logic
if (app.Environment.EnvironmentName != "Testing")
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    bool dbConnected = false;
    int retryCount = 0;
    const int maxRetries = 10;
    const int retryDelaySeconds = 5;

    while (!dbConnected && retryCount < maxRetries)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            startupLogger.LogInformation("Attempting database connection and migrations (Attempt {Attempt}/{MaxRetries})...", retryCount + 1, maxRetries);
            dbContext.Database.Migrate();
            dbConnected = true;
            startupLogger.LogInformation("Database connection successful and migrations applied.");
        }
        catch (NpgsqlException ex)
        {
            startupLogger.LogError(ex, "Database connection failed: {ErrorMessage}", ex.Message);
            retryCount++;
            if (retryCount < maxRetries)
            {
                startupLogger.LogInformation("Retrying in {Delay} seconds...", retryDelaySeconds);
                Thread.Sleep(TimeSpan.FromSeconds(retryDelaySeconds));
            }
            else
            {
                startupLogger.LogCritical("Failed to connect to the database after {MaxRetries} retries. Application will terminate.", maxRetries);
                throw;
            }
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Unexpected error during database setup: {ErrorMessage}", ex.Message);
            retryCount++;
            if (retryCount < maxRetries)
            {
                startupLogger.LogInformation("Retrying in {Delay} seconds...", retryDelaySeconds);
                Thread.Sleep(TimeSpan.FromSeconds(retryDelaySeconds));
            }
            else
            {
                startupLogger.LogCritical("Database operations failed after {MaxRetries} retries.", maxRetries);
                throw;
            }
        }
    }
}
#endregion

#region Configure HTTP Pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapHealthEndpoints();
app.MapAuthEndpoints();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
#endregion

app.Run();

// Expose Program type so WebApplicationFactory<Program> works in tests
public partial class Program;
