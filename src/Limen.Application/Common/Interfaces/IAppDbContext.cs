using Limen.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<AdminSession> AdminSessions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}
