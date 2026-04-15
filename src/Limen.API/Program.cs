using Limen.API.Endpoints;
using Limen.API.Middleware;
using Limen.Application.Common;
using Limen.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

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

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthEndpoints();
    app.MapAuthEndpoints();

    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Limen.API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Expose Program type so WebApplicationFactory<Program> works in tests
public partial class Program;
