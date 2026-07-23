namespace InvoicePK.DTOs.Dashboard;

public record CurrencyBreakdown(
    string Currency,
    decimal TotalRevenue,
    decimal PendingAmount,
    int InvoiceCount
);
