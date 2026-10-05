using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace C2C.Web.Services;

/// <summary>Sends notification emails over SMTP. Does nothing (just logs) when Smtp:Host is not configured.</summary>
public class Mailer(IConfiguration config, ILogger<Mailer> log)
{
    public async Task NotifyAsync(string subject, string body, string? replyTo = null)
    {
        var host = config["Smtp:Host"];
        var to = config["Smtp:To"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(to))
        {
            log.LogInformation("SMTP not configured; skipping email '{Subject}'", subject);
            return;
        }
        try
        {
            var msg = new MimeMessage();
            var from = config["Smtp:From"];
            if (string.IsNullOrWhiteSpace(from)) from = config["Smtp:User"];
            if (string.IsNullOrWhiteSpace(from)) from = "website@localhost";
            msg.From.Add(MailboxAddress.Parse(from));
            foreach (var addr in to.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                msg.To.Add(MailboxAddress.Parse(addr));
            if (replyTo is not null && MailboxAddress.TryParse(replyTo, out var rt)) msg.ReplyTo.Add(rt);
            msg.Subject = subject;
            msg.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            var port = config.GetValue("Smtp:Port", 587);
            await client.ConnectAsync(host, port, port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
            var user = config["Smtp:User"];
            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, config["Smtp:Password"] ?? "");
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // The submission is already saved in the database; never fail the visitor's request over email.
            log.LogError(ex, "Failed to send notification email '{Subject}'", subject);
        }
    }
}
