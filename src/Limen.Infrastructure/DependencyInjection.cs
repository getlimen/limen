using Limen.Application.Common.Interfaces;
using Limen.Application.Common.Options;
using Limen.Application.Services;
using Limen.Infrastructure.Agents;
using Limen.Infrastructure.Clock;
using Limen.Infrastructure.Deployments;
using Limen.Infrastructure.Jobs;
using Limen.Infrastructure.Persistence;
using Limen.Infrastructure.Proxies;
using Limen.Infrastructure.Registry;
using Limen.Infrastructure.Tunnels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

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
        services.AddSingleton<IProxyConnectionRegistry, ProxyConnectionRegistry>();
        services.AddScoped<ITunnelCoordinator, TunnelCoordinator>();
        services.AddScoped<ProxyConfigPusher>();
        services.AddHttpClient<IForculusClient, ForculusHttpClient>(c =>
            c.BaseAddress = new Uri(config["Forculus:BaseUrl"] ?? "http://forculus:3004"));
        services.Configure<WgServerSettings>(config.GetSection("Wg"));
        services.Configure<ForculusSettings>(config.GetSection("Forculus"));

        services.AddScoped<IDeploymentDispatcher, DeploymentDispatcher>();
        services.AddHttpClient<IRegistryClient, RegistryClient>();

        services.AddQuartz(q =>
        {
            var key = new JobKey("RegistryPoll");
            q.AddJob<RegistryPollJob>(opts => opts.WithIdentity(key));
            q.AddTrigger(t => t
                .ForJob(key)
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }
}
