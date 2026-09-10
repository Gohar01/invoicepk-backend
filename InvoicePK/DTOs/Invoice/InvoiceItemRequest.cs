using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Invoice;

public record InvoiceItemRequest(
    [Required, MaxLength(1000)] string Description,
    [Required, Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Quantity must be greater than 0.")] decimal Quantity,
    [Required, Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Unit price must be greater than 0.")] decimal UnitPrice
);