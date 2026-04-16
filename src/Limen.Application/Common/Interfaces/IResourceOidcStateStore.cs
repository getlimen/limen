namespace Limen.Application.Common.Interfaces;

public interface IResourceOidcStateStore
{
    string CreateState(Guid routeId, string returnTo);
    (Guid RouteId, string ReturnTo)? ConsumeState(string state);
}
