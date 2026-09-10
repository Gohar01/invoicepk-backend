using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Invoice;

public record UpdateInvoiceRequest(
    int? ClientId,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    [MaxLength(10)] string? Currency,
    decimal? GSTPercent,
    [MaxLength(500)] string? Notes,
    List<InvoiceItemRequest>? Items
);