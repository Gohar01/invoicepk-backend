namespace InvoicePK.DTOs.Client;

public record UpdateClientRequest(
    string? Name,
    string? Email,
    string? Phone,
    string? Address
);