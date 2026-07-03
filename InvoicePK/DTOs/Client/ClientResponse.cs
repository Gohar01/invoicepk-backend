namespace InvoicePK.DTOs.Client;

public record ClientResponse(
    int Id,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    DateTime CreatedAt,
    int TotalInvoices
);