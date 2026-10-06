using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace C2C.Web.Services;

/// <summary>
/// Sends notification emails through the Resend HTTP API. Does nothing (just logs) when Resend:ApiKey is not set.
/// Submissions are always saved before this runs, so a failure here never loses an inquiry.
/// </summary>
public class Mailer(IConfiguration config, ILogger<Mailer> log)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task NotifyAsync(string subject, string body, string? replyTo = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config["Resend:ApiKey"]))
            {
                log.LogInformation("Resend not configured; skipping email '{Subject}'", subject);
                return;
            }

            var to = (config["Resend:To"] ?? "")
                .Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var from = config["Resend:From"];
            if (to.Length == 0 || string.IsNullOrWhiteSpace(from))
            {
                log.LogWarning("Resend:From or Resend:To is missing; skipping email '{Subject}'", subject);
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
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to send notification email '{Subject}'", subject);
        }
    }
}
