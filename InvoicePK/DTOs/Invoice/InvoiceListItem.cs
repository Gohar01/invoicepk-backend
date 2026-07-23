namespace InvoicePK.DTOs.Invoice;

public record InvoiceListItem(
    int Id,
    string InvoiceNumber,
    string ClientName,
    string Currency,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt
);