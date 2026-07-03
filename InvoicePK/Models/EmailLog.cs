namespace InvoicePK.Models;

public class EmailLog
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty;   // Invoice | Reminder
    public string Status { get; set; } = string.Empty; // Sent | Failed

    // Navigation
    public Invoice Invoice { get; set; } = null!;
}