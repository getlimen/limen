using Limen.Application.Common.Interfaces;
using Limen.Domain.Services;
using Mediator;

namespace Limen.Application.Commands.Services;

public sealed record CreateServiceCommand(
    string Name,
    Guid TargetNodeId,
    string ContainerName,
    int InternalPort,
    string Image,
    bool AutoDeploy) : ICommand<Guid>;

internal sealed class CreateServiceCommandHandler : ICommandHandler<CreateServiceCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public CreateServiceCommandHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<Guid> Handle(CreateServiceCommand cmd, CancellationToken ct)
    {
        var service = new Service
        {
            Id = Guid.NewGuid(),
            Name = cmd.Name,
            TargetNodeId = cmd.TargetNodeId,
            ContainerName = cmd.ContainerName,
            InternalPort = cmd.InternalPort,
            Image = cmd.Image,
            AutoDeploy = cmd.AutoDeploy,
            CreatedAt = _clock.UtcNow,
        };
        _db.Services.Add(service);
        await _db.SaveChangesAsync(ct);
        return service.Id;
    }
}
