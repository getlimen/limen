using Limen.Application.Common.Interfaces;
using Limen.Domain.Nodes;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Nodes;

public sealed record NodeDto(Guid Id, string Name, string[] Roles, string Status, DateTimeOffset? LastSeenAt);

public sealed record ListNodesQuery() : IQuery<IReadOnlyList<NodeDto>>;

internal sealed class ListNodesQueryHandler : IQueryHandler<ListNodesQuery, IReadOnlyList<NodeDto>>
{
    private readonly IAppDbContext _db;
    public ListNodesQueryHandler(IAppDbContext db) { _db = db; }

    public async ValueTask<IReadOnlyList<NodeDto>> Handle(ListNodesQuery q, CancellationToken ct)
    {
        return await _db.Nodes
            .Select(n => new NodeDto(n.Id, n.Name, n.Roles, n.Status.ToString(), n.LastSeenAt))
            .ToListAsync(ct);
    }
}
