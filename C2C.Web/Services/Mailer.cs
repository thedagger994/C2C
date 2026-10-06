using System.Net.Http.Headers;
using System.Net.Http.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace C2C.Web.Services;

/// <summary>
/// Sends notification emails. Uses the Resend HTTP API when Resend:ApiKey is set (works on hosts that block SMTP
/// and with Microsoft 365 accounts that no longer allow password SMTP); otherwise falls back to plain SMTP when
/// Smtp:Host is set. With neither configured it just logs. Submissions are always saved first, so a failure here
/// never loses an inquiry.
/// </summary>
public class Mailer(IConfiguration config, ILogger<Mailer> log)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task NotifyAsync(string subject, string body, string? replyTo = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(config["Resend:ApiKey"]))
                await SendViaResendAsync(subject, body, replyTo);
            else if (!string.IsNullOrWhiteSpace(config["Smtp:Host"]))
                await SendViaSmtpAsync(subject, body, replyTo);
            else
                log.LogInformation("No email provider configured; skipping email '{Subject}'", subject);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to send notification email '{Subject}'", subject);
        }
    }

    private static string[] SplitAddresses(string? list) =>
        (list ?? "").Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task SendViaResendAsync(string subject, string body, string? replyTo)
    {
        var to = SplitAddresses(config["Resend:To"] ?? config["Smtp:To"]);
        var from = config["Resend:From"];
        if (to.Length == 0 || string.IsNullOrWhiteSpace(from))
        {
            log.LogWarning("Resend is configured but Resend:From or Resend:To is missing; skipping email '{Subject}'", subject);
            return;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["Resend:ApiKey"]);
        req.Content = JsonContent.Create(new
        {
            from,
            to,
            subject,
            text = body,
            reply_to = string.IsNullOrWhiteSpace(replyTo) ? null : new[] { replyTo }
        });

        using var res = await Http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            log.LogError("Resend rejected email '{Subject}': {Status} {Body}", subject, (int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task SendViaSmtpAsync(string subject, string body, string? replyTo)
    {
        var host = config["Smtp:Host"]!;
        var to = SplitAddresses(config["Smtp:To"]);
        if (to.Length == 0)
        {
            log.LogWarning("Smtp:To is missing; skipping email '{Subject}'", subject);
            return;
        }

        var msg = new MimeMessage();
        var from = config["Smtp:From"];
        if (string.IsNullOrWhiteSpace(from)) from = config["Smtp:User"];
        if (string.IsNullOrWhiteSpace(from)) from = "website@localhost";
        msg.From.Add(MailboxAddress.Parse(from));
        foreach (var addr in to) msg.To.Add(MailboxAddress.Parse(addr));
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
}
