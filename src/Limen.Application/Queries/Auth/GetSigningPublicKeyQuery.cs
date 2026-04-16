using Limen.Application.Common.Interfaces;
using Mediator;

namespace Limen.Application.Queries.Auth;

public sealed record SigningPublicKeyDto(string Kid, string Alg, string PublicKeyBase64);

public sealed record GetSigningPublicKeyQuery() : IQuery<SigningPublicKeyDto>;

internal sealed class GetSigningPublicKeyQueryHandler
    : IQueryHandler<GetSigningPublicKeyQuery, SigningPublicKeyDto>
{
    private readonly ITokenSigner _signer;

    public GetSigningPublicKeyQueryHandler(ITokenSigner signer)
    {
        _signer = signer;
    }

    public ValueTask<SigningPublicKeyDto> Handle(GetSigningPublicKeyQuery query, CancellationToken ct)
    {
        var dto = new SigningPublicKeyDto(
            _signer.KeyId,
            "EdDSA",
            Convert.ToBase64String(_signer.PublicKeyBytes));
        return ValueTask.FromResult(dto);
    }
}
