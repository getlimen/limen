using Microsoft.Extensions.Options;

namespace Limen.Application.Common.Options;

public sealed class AuthSettingsValidator : IValidateOptions<AuthSettings>
{
    public ValidateOptionsResult Validate(string? name, AuthSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKeyPath))
        {
            return ValidateOptionsResult.Fail("Auth:SigningKeyPath required");
        }

        // PublicBaseUrl is only required when the magic-link / allowlist flow is used,
        // so we don't fail startup — the handler throws at send time instead.
        return ValidateOptionsResult.Success;
    }
}
