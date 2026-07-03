using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Invoice;

public record InvoiceItemRequest(
    [Required] string Description,
    [Required, Range(0.01, double.MaxValue)] decimal Quantity,
    [Required, Range(0.01, double.MaxValue)] decimal UnitPrice
);