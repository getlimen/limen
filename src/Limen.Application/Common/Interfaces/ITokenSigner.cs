namespace Limen.Application.Common.Interfaces;

public interface ITokenSigner
{
    string SignJwt(IDictionary<string, object> header, IDictionary<string, object> payload);
    bool Verify(string jwt);
    byte[] PublicKeyBytes { get; }
    string KeyId { get; }  // e.g., "limen-2026-04"
}
