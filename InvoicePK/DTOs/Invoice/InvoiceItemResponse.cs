namespace InvoicePK.DTOs.Invoice;

public record InvoiceItemResponse(
    int Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal SubTotal
);