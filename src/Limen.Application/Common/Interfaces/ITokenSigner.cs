namespace Limen.Application.Common.Interfaces;

public interface ITokenSigner
{
    string SignJwt(IDictionary<string, object> header, IDictionary<string, object> payload);
    byte[] PublicKeyBytes { get; }
    string KeyId { get; }  // e.g., "limen-2026-04"
}
