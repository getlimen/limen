namespace Limen.Application.Common.Options;

public sealed class WgServerSettings
{
    public string PublicKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "localhost:51820";
}
