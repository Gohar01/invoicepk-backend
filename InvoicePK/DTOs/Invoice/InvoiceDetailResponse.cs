using InvoicePK.DTOs.Client;

namespace InvoicePK.DTOs.Invoice;

public record InvoiceDetailResponse(
    int Id,
    string InvoiceNumber,
    ClientResponse Client,
    string Currency,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal GSTPercent,
    decimal SubTotal,
    decimal GSTAmount,
    decimal TotalAmount,
    string Status,
    string? Notes,
    List<InvoiceItemResponse> Items,
    DateTime CreatedAt
);