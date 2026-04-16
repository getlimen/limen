namespace Limen.Application.Common.Options;

public sealed class AuthSettings
{
    public string SigningKeyPath { get; set; } = "/data/signing-key.bin";
    public string SigningKeyId { get; set; } = "limen-default";
    public int TokenTtlMinutes { get; set; } = 15;
    public int MagicLinkTtlMinutes { get; set; } = 15;
    public string? PublicBaseUrl { get; set; }  // e.g., "https://limen.example.com" — used for magic link URLs
    public SmtpSettings? Smtp { get; set; }

    /// <summary>
    /// When SMTP is not configured, log magic link URLs at Debug level instead of throwing.
    /// OPERATOR-VISIBLE dev fallback — must NEVER be enabled in production.
    /// Defaults to false so production mis-configurations fail loudly.
    /// </summary>
    public bool LogMagicLinksInDev { get; set; } = false;

    /// <summary>
    /// Sets the Secure flag on the limen_session cookie. Default true.
    /// Disable only in local HTTP development environments.
    /// </summary>
    public bool CookieSecure { get; set; } = true;
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
