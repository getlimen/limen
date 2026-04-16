using Limen.Application.Common.Interfaces;
using Limen.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace Limen.Application.Services;

public sealed class JwtBuilder
{
    private readonly ITokenSigner _signer;
    private readonly IClock _clock;
    private readonly IOptions<AuthSettings> _opt;

    public JwtBuilder(ITokenSigner signer, IClock clock, IOptions<AuthSettings> opt)
    {
        _signer = signer;
        _clock = clock;
        _opt = opt;
    }

    public (string Jwt, Guid Jti, DateTimeOffset Exp) Build(
        Guid routeId, string subject, string authMethod, string cookieScope)
    {
        var jti = Guid.NewGuid();
        var now = _clock.UtcNow;
        var exp = now.AddMinutes(_opt.Value.TokenTtlMinutes);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "EdDSA",
            ["typ"] = "JWT",
            ["kid"] = _signer.KeyId,
        };
        var payload = new Dictionary<string, object>
        {
            ["jti"] = jti.ToString(),
            ["sub"] = subject,
            ["routeId"] = routeId.ToString(),
            ["authMethod"] = authMethod,
            ["cookieScope"] = cookieScope,
            ["iss"] = "limen",
            ["aud"] = routeId.ToString(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(),
        };
        return (_signer.SignJwt(header, payload), jti, exp);
    }
}
