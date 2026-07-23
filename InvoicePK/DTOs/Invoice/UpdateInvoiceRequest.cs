namespace InvoicePK.DTOs.Invoice;

public record UpdateInvoiceRequest(
    int? ClientId,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? GSTPercent,
    string? Notes,
    List<InvoiceItemRequest>? Items
);