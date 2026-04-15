using Limen.Application.Common.Interfaces;
using Limen.Application.Common.Options;
using Limen.Application.Services;
using Limen.Infrastructure.Agents;
using Limen.Infrastructure.Clock;
using Limen.Infrastructure.Persistence;
using Limen.Infrastructure.Tunnels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Limen.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");

        services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cs));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAgentConnectionRegistry, AgentConnectionRegistry>();
        services.AddScoped<ITunnelCoordinator, TunnelCoordinator>();
        services.AddHttpClient<IForculusClient, ForculusHttpClient>(c =>
            c.BaseAddress = new Uri(config["Forculus:BaseUrl"] ?? "http://forculus:3004"));
        services.Configure<WgServerSettings>(config.GetSection("Wg"));
        services.Configure<ForculusSettings>(config.GetSection("Forculus"));
        return services;
    }
}
