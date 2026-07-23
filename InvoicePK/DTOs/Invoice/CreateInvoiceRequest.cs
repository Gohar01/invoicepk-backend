using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Invoice;

public record CreateInvoiceRequest(
    [Required] int ClientId,
    [Required] DateOnly IssueDate,
    [Required] DateOnly DueDate,
    [Required] string Currency,
    [Range(0, 100)] decimal GSTPercent,
    string? Notes,
    [Required, MinLength(1)] List<InvoiceItemRequest> Items
);