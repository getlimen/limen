namespace Limen.Application.Common.Options;

public sealed class ForculusSettings
{
    public string PrivateKey { get; set; } = string.Empty;
    public int ListenPort { get; set; } = 51820;
    public string InterfaceAddress { get; set; } = "10.42.0.1/24";
}
