namespace Limen.Application.Common.Options;

public sealed class AuthSettings
{
    public string SigningKeyPath { get; set; } = "/data/signing-key.bin";
    public string SigningKeyId { get; set; } = "limen-default";
    public int TokenTtlMinutes { get; set; } = 15;
    public int MagicLinkTtlMinutes { get; set; } = 15;
    public string? PublicBaseUrl { get; set; }  // e.g., "https://limen.example.com" — used for magic link URLs
    public SmtpSettings? Smtp { get; set; }
}

public sealed class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public bool UseStartTls { get; set; } = true;
}
