namespace InvoicePK.DTOs.Payments;

public class SubmitPaymentProofRequest
{
    public string SenderName { get; set; } = string.Empty;
    public string? SenderPhone { get; set; }
    public string? AccountEmail { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Plan { get; set; } = "Lifetime Pro";
    public decimal Amount { get; set; } = 2999;
}

public class ApprovePaymentProofRequest
{
    public string AdminSecret { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
}
