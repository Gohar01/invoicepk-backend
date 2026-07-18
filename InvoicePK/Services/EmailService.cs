using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using InvoicePK.Models;

namespace InvoicePK.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config) => _config = config;

    public async Task<bool> SendInvoiceAsync(Invoice invoice, User user, byte[] pdfBytes)
    {
        try
        {
            var message = new MimeMessage();

            // From
            message.From.Add(new MailboxAddress(
                user.BusinessName ?? user.FullName,
                _config["Email:From"]!
            ));

            // To (client email)
            if (string.IsNullOrEmpty(invoice.Client.Email))
                throw new Exception("Client has no email address.");

            message.To.Add(new MailboxAddress(invoice.Client.Name, invoice.Client.Email));

            // Subject
            message.Subject = $"Invoice {invoice.InvoiceNumber} from {user.BusinessName ?? user.FullName}";

            // Body + PDF attachment
            var builder = new BodyBuilder
            {
                HtmlBody = BuildEmailHtml(invoice, user),
                TextBody = BuildEmailText(invoice, user)
            };

            builder.Attachments.Add(
                $"{invoice.InvoiceNumber}.pdf",
                pdfBytes,
                ContentType.Parse("application/pdf")
            );

            message.Body = builder.ToMessageBody();

            // Send via SMTP
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _config["Email:SmtpHost"],
                int.Parse(_config["Email:SmtpPort"]!),
                SecureSocketOptions.StartTls
            );
            await smtp.AuthenticateAsync(
                _config["Email:Username"],
                _config["Email:Password"]
            );
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Email error: {ex.Message}");
            return false;
        }
    }

    // ── Email reminder ────────────────────────────
    public async Task<bool> SendReminderAsync(Invoice invoice, User user, byte[] pdfBytes)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                user.BusinessName ?? user.FullName,
                _config["Email:From"]!
            ));
            message.To.Add(new MailboxAddress(invoice.Client.Name, invoice.Client.Email!));
            message.Subject = $"Payment Reminder — Invoice {invoice.InvoiceNumber} is Overdue";

            var builder = new BodyBuilder
            {
                HtmlBody = BuildReminderHtml(invoice, user),
                TextBody = $"Reminder: Invoice {invoice.InvoiceNumber} of PKR {invoice.TotalAmount:N0} was due on {invoice.DueDate}."
            };
            builder.Attachments.Add($"{invoice.InvoiceNumber}.pdf", pdfBytes,
                ContentType.Parse("application/pdf"));

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_config["Email:SmtpHost"],
                int.Parse(_config["Email:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_config["Email:Username"], _config["Email:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Reminder email error: {ex.Message}");
            return false;
        }
    }

    // ── Password reset email ────────────────────────
    public async Task<bool> SendPasswordResetAsync(User user, string resetLink)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("InvoicePK", _config["Email:From"]!));
            message.To.Add(new MailboxAddress(user.FullName, user.Email));
        message.Subject = "Reset Your InvoicePK Password";

        var builder = new BodyBuilder
        {
            HtmlBody = $"""
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
                """,
            TextBody = $"Reset your password: {resetLink} (expires in 1 hour)"
        };

        message.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_config["Email:SmtpHost"],
            int.Parse(_config["Email:SmtpPort"]!), SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_config["Email:Username"], _config["Email:Password"]);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);

        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Password reset email error: {ex.Message}");
        return false;
    }
  }

    // ── HTML Templates ────────────────────────────
    private static string BuildEmailHtml(Invoice invoice, User user) => $"""
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
                <td style="padding:10px;text-align:right;"><strong>PKR {invoice.TotalAmount:N0}</strong></td>
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

    private static string BuildEmailText(Invoice invoice, User user) =>
        $"Dear {invoice.Client.Name},\n\n" +
        $"Please find attached invoice {invoice.InvoiceNumber}.\n" +
        $"Amount: PKR {invoice.TotalAmount:N0}\n" +
        $"Due Date: {invoice.DueDate:dd MMM yyyy}\n\n" +
        $"Thank you,\n{user.FullName}";

    private static string BuildReminderHtml(Invoice invoice, User user) => $"""
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
                <td style="padding:10px;text-align:right;"><strong>PKR {invoice.TotalAmount:N0}</strong></td>
              </tr>
            </table>
            <p>Please arrange payment at your earliest convenience.</p>
            <p>Regards,<br><strong>{user.FullName}</strong><br>{user.BusinessName ?? ""}</p>
          </div>
        </body>
        </html>
        """;
}
