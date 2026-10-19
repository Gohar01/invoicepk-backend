namespace InvoicePK.Models;

public class PaymentProof
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderPhone { get; set; }
    public string? AccountEmail { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Plan { get; set; } = "Lifetime Pro";
    public decimal Amount { get; set; } = 2999;
    public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
