using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InvoicePK.Models;
using InvoicePK.Helpers;

namespace InvoicePK.Services;

// Uses Resend's HTTPS API instead of SMTP — this works on Railway's Free/Hobby
// plans (and any host), since Railway blocks outbound SMTP ports (465/587/2525)
// on everything below the Pro plan. HTTPS (port 443) is never blocked.
public class EmailService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public EmailService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _http = httpClientFactory.CreateClient();
        _http.BaseAddress = new Uri("https://api.resend.com/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["Resend:ApiKey"]);
    }

    // ── Send Invoice ──────────────────────────────
    public async Task<(bool Success, string? Error)> SendInvoiceAsync(Invoice invoice, User user, byte[] pdfBytes)
    {
        if (string.IsNullOrEmpty(invoice.Client.Email))
            return (false, "Client has no email address.");

        var html = BuildInvoiceHtml(invoice, user);
        return await SendAsync(
            to: invoice.Client.Email,
            toName: invoice.Client.Name,
            fromName: user.BusinessName ?? user.FullName,
            subject: $"Invoice {invoice.InvoiceNumber} from {user.BusinessName ?? user.FullName}",
            html: html,
            attachmentName: $"{invoice.InvoiceNumber}.pdf",
            attachmentBytes: pdfBytes
        );
    }

    // ── Send Reminder ─────────────────────────────
    public async Task<(bool Success, string? Error)> SendReminderAsync(Invoice invoice, User user, byte[] pdfBytes)
    {
        if (string.IsNullOrEmpty(invoice.Client.Email))
            return (false, "Client has no email address.");

        var html = BuildReminderHtml(invoice, user);
        return await SendAsync(
            to: invoice.Client.Email,
            toName: invoice.Client.Name,
            fromName: user.BusinessName ?? user.FullName,
            subject: $"Payment Reminder — Invoice {invoice.InvoiceNumber} is Overdue",
            html: html,
            attachmentName: $"{invoice.InvoiceNumber}.pdf",
            attachmentBytes: pdfBytes
        );
    }

    // ── Send Password Reset ───────────────────────
    public async Task<(bool Success, string? Error)> SendPasswordResetAsync(User user, string resetLink)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;color:#333;">
              <div style="background:#00C16A;padding:20px;border-radius:8px 8px 0 0;">
                <h1 style="color:white;margin:0;font-size:22px;">InvoicePK</h1>
              </div>
              <div style="background:#f9f9f9;padding:24px;border:1px solid #eee;">
                <p>Hi <strong>{user.FullName}</strong>,</p>
                <p>We received a request to reset your password. Click the button below to set a new one:</p>
                <div style="text-align:center;margin:24px 0;">
                  <a href="{resetLink}"
                     style="background:#00C16A;color:white;padding:12px 28px;border-radius:8px;
                            text-decoration:none;font-weight:bold;display:inline-block;">
                    Reset Password
                  </a>
                </div>
                <p style="font-size:13px;color:#888;">This link expires in 1 hour. If you didn't request this, you can safely ignore this email.</p>
                <p style="font-size:13px;color:#888;">Or copy this link: {resetLink}</p>
              </div>
            </body>
            </html>
            """;

        return await SendAsync(
            to: user.Email,
            toName: user.FullName,
            fromName: "InvoicePK",
            subject: "Reset Your InvoicePK Password",
            html: html
        );
    }

    // ── Core send method (calls Resend API) ───────
    private async Task<(bool Success, string? Error)> SendAsync(
        string to, string toName, string fromName, string subject, string html,
        string? attachmentName = null, byte[]? attachmentBytes = null)
    {
        try
        {
            var fromEmail = _config["Resend:FromEmail"] ?? "onboarding@resend.dev";

            var payload = new Dictionary<string, object?>
            {
                ["from"]    = $"{fromName} <{fromEmail}>",
                ["to"]      = new[] { to },
                ["subject"] = subject,
                ["html"]    = html,
            };

            if (attachmentBytes != null && attachmentName != null)
            {
                payload["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["filename"] = attachmentName,
                        ["content"]  = Convert.ToBase64String(attachmentBytes),
                    }
                };
            }

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("emails", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Resend API error ({response.StatusCode}): {errorBody}");

                // Parse Resend's error message so the user sees something useful
                string friendlyError = "Failed to send email.";
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(errorBody);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp))
                        friendlyError = msgProp.GetString() ?? friendlyError;
                }
                catch { /* fall back to generic message if parsing fails */ }

                return (false, friendlyError);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Email send error: {ex.Message}");
            return (false, "Could not reach the email service. Please try again.");
        }
    }

    // ── HTML Templates ────────────────────────────
    private static string BuildInvoiceHtml(Invoice invoice, User user)
    {
        var symbol = CurrencyHelper.GetSymbol(invoice.Currency);
        return $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;color:#333;">
          <div style="background:#00C16A;padding:20px;border-radius:8px 8px 0 0;">
            <h1 style="color:white;margin:0;font-size:24px;">
              {user.BusinessName ?? user.FullName}
            </h1>
          </div>
          <div style="background:#f9f9f9;padding:24px;border:1px solid #eee;">
            <p>Dear <strong>{invoice.Client.Name}</strong>,</p>
            <p>Please find attached invoice <strong>{invoice.InvoiceNumber}</strong> for your records.</p>
            <table style="width:100%;border-collapse:collapse;margin:20px 0;">
              <tr style="background:#1a1a1a;color:white;">
                <td style="padding:10px;">Invoice Number</td>
                <td style="padding:10px;text-align:right;"><strong>{invoice.InvoiceNumber}</strong></td>
              </tr>
              <tr style="background:#f0f0f0;">
                <td style="padding:10px;">Issue Date</td>
                <td style="padding:10px;text-align:right;">{invoice.IssueDate:dd MMM yyyy}</td>
              </tr>
              <tr>
                <td style="padding:10px;">Due Date</td>
                <td style="padding:10px;text-align:right;">{invoice.DueDate:dd MMM yyyy}</td>
              </tr>
              <tr style="background:#00C16A;color:white;">
                <td style="padding:10px;"><strong>Total Amount</strong></td>
                <td style="padding:10px;text-align:right;"><strong>{symbol} {invoice.TotalAmount:N0}</strong></td>
              </tr>
            </table>
            <p>Please make payment by <strong>{invoice.DueDate:dd MMM yyyy}</strong>.</p>
            <p>If you have any questions, feel free to contact us.</p>
            <p>Thank you for your business!</p>
            <p>Regards,<br><strong>{user.FullName}</strong><br>
            {user.BusinessName ?? ""}<br>{user.Phone ?? ""}</p>
          </div>
          <div style="text-align:center;padding:12px;font-size:11px;color:#aaa;">
            Powered by InvoicePK
          </div>
        </body>
        </html>
        """;
    }

    private static string BuildReminderHtml(Invoice invoice, User user)
    {
        var symbol = CurrencyHelper.GetSymbol(invoice.Currency);
        return $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;color:#333;">
          <div style="background:#E53E3E;padding:20px;border-radius:8px 8px 0 0;">
            <h1 style="color:white;margin:0;font-size:22px;">Payment Reminder</h1>
          </div>
          <div style="background:#f9f9f9;padding:24px;border:1px solid #eee;">
            <p>Dear <strong>{invoice.Client.Name}</strong>,</p>
            <p>This is a friendly reminder that invoice <strong>{invoice.InvoiceNumber}</strong>
               was due on <strong>{invoice.DueDate:dd MMM yyyy}</strong> and is now overdue.</p>
            <table style="width:100%;border-collapse:collapse;margin:20px 0;">
              <tr style="background:#E53E3E;color:white;">
                <td style="padding:10px;"><strong>Amount Due</strong></td>
                <td style="padding:10px;text-align:right;"><strong>{symbol} {invoice.TotalAmount:N0}</strong></td>
              </tr>
            </table>
            <p>Please arrange payment at your earliest convenience.</p>
            <p>Regards,<br><strong>{user.FullName}</strong><br>{user.BusinessName ?? ""}</p>
          </div>
        </body>
        </html>
        """;
    }
}
