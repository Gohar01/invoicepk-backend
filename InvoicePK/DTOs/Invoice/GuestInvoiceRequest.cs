using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Invoice;

public record GuestInvoiceRequest(
    [Required, MaxLength(100)] string BusinessName,
    [MaxLength(250)] string? SenderAddress,
    [MaxLength(100)] string? SenderPhone,
    [MaxLength(100)] string? SenderEmail,
    [MaxLength(20)] string? SenderNTN,
    string? LogoUrl,

    [Required, MaxLength(100)] string ClientName,
    [MaxLength(250)] string? ClientAddress,
    [MaxLength(100)] string? ClientPhone,
    [MaxLength(100)] string? ClientEmail,

    [Required, MaxLength(10)] string Currency,
    [Required] DateOnly IssueDate,
    [Required] DateOnly DueDate,
    [Range(0, 100)] decimal GSTPercent,
    [MaxLength(500)] string? Notes,
    [Required, MinLength(1)] List<InvoiceItemRequest> Items
);
