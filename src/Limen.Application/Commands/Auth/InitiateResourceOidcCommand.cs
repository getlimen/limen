using System.Security.Cryptography;
using Limen.Application.Common.Interfaces;
using Mediator;

namespace Limen.Application.Commands.Auth;

public sealed record InitiateResourceOidcCommand(Guid RouteId, string ReturnTo) : ICommand<string>;

internal sealed class InitiateResourceOidcCommandHandler : ICommandHandler<InitiateResourceOidcCommand, string>
{
    private readonly IResourceOidcStateStore _store;

    public InitiateResourceOidcCommandHandler(IResourceOidcStateStore store)
    {
        _store = store;
    }

    public ValueTask<string> Handle(InitiateResourceOidcCommand cmd, CancellationToken ct)
    {
        var state = _store.CreateState(cmd.RouteId, cmd.ReturnTo);
        return ValueTask.FromResult(state);
    }
}
