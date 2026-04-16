using Limen.Application.Common.Interfaces;
using Limen.Application.Common.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Limen.Infrastructure.Auth;

public sealed class MagicLinkSender : IMagicLinkSender
{
    private readonly IOptions<AuthSettings> _opt;
    private readonly ILogger<MagicLinkSender> _logger;

    public MagicLinkSender(IOptions<AuthSettings> opt, ILogger<MagicLinkSender> logger)
    {
        _opt = opt;
        _logger = logger;
    }

    public async Task SendAsync(string email, string magicUrl, string routeHostname, CancellationToken ct)
    {
        var smtp = _opt.Value.Smtp;
        if (smtp is null || string.IsNullOrEmpty(smtp.Host))
        {
            _logger.LogInformation("Magic link for {Email} on {Hostname}: {Url}", email, routeHostname, magicUrl);
            return;
        }

        var ttl = _opt.Value.MagicLinkTtlMinutes;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = $"Sign in to {routeHostname}";
        message.Body = new TextPart("plain")
        {
            Text = $"Click this link to sign in to {routeHostname}: {magicUrl}\n\nThis link expires in {ttl} minutes."
        };

        using var client = new SmtpClient();
        var secureSocketOptions = smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(smtp.Host, smtp.Port, secureSocketOptions, ct);
        if (!string.IsNullOrEmpty(smtp.Username) && !string.IsNullOrEmpty(smtp.Password))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password, ct);
        }
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
