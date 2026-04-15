namespace Limen.Application.Common.Interfaces;

public interface IRegistryClient
{
    Task<string?> GetManifestDigestAsync(string image, CancellationToken ct);
}
