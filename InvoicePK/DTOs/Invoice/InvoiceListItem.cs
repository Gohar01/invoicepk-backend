namespace InvoicePK.DTOs.Invoice;

public record InvoiceListItem(
    int Id,
    string InvoiceNumber,
    string ClientName,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt
);