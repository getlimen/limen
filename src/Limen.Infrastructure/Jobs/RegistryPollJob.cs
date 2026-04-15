using Limen.Application.Commands.Deployments;
using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Limen.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class RegistryPollJob : IJob
{
    private readonly IAppDbContext _db;
    private readonly IRegistryClient _registry;
    private readonly IMediator _mediator;
    private readonly ILogger<RegistryPollJob> _log;

    public RegistryPollJob(
        IAppDbContext db,
        IRegistryClient registry,
        IMediator mediator,
        ILogger<RegistryPollJob> log)
    {
        _db = db;
        _registry = registry;
        _mediator = mediator;
        _log = log;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var services = await _db.Services
            .Where(s => s.AutoDeploy)
            .ToListAsync(ct);

        foreach (var service in services)
        {
            try
            {
                var digest = await _registry.GetManifestDigestAsync(service.Image, ct);
                if (digest is null)
                {
                    _log.LogDebug("Could not get digest for {Image}; skipping", service.Image);
                    continue;
                }

                var latestDigest = await _db.Deployments
                    .Where(d => d.ServiceId == service.Id)
                    .OrderByDescending(d => d.QueuedAt)
                    .Select(d => d.ImageDigest)
                    .FirstOrDefaultAsync(ct);

                if (latestDigest == digest)
                {
                    continue;
                }

                var previousDeploymentId = await _db.Deployments
                    .Where(d => d.ServiceId == service.Id)
                    .OrderByDescending(d => d.QueuedAt)
                    .Select(d => (Guid?)d.Id)
                    .FirstOrDefaultAsync(ct);

                var tag = ExtractTag(service.Image);

                _log.LogInformation("New digest {Digest} detected for {Image}; queuing deployment", digest, service.Image);

                await _mediator.Send(
                    new CreateDeploymentCommand(service.Id, digest, tag, previousDeploymentId),
                    ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Registry poll failed for service {ServiceId} ({Image})", service.Id, service.Image);
            }
        }
    }

    private static string ExtractTag(string image)
    {
        var atIdx = image.IndexOf('@');
        if (atIdx >= 0)
        {
            return image[(atIdx + 1)..];
        }

        var colonIdx = image.LastIndexOf(':');
        var slashIdx = image.LastIndexOf('/');
        return colonIdx > slashIdx ? image[(colonIdx + 1)..] : "latest";
    }
}
