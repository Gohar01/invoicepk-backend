using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Invoice;

public record InvoiceItemRequest(
    [Required] string Description,
    [Required, Range(1, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    decimal Quantity,
    [Required, Range(1, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
    decimal UnitPrice
);