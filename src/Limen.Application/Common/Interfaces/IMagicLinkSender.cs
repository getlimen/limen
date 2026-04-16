namespace Limen.Application.Common.Interfaces;

public interface IMagicLinkSender
{
    Task SendAsync(string email, string magicUrl, string routeHostname, CancellationToken ct);
}
