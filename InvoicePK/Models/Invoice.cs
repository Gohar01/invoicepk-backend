namespace InvoicePK.Models;

public class Invoice
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ClientId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "PKR";
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal GSTPercent { get; set; } = 0;
    public decimal SubTotal { get; set; } = 0;
    public decimal GSTAmount { get; set; } = 0;
    public decimal TotalAmount { get; set; } = 0;
    public string Status { get; set; } = "Draft";        // Draft | Sent | Paid | Overdue
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();
}