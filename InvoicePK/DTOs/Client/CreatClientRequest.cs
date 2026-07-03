using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Client;

public record CreateClientRequest(
    [Required] string Name,
    string? Email,
    string? Phone,
    string? Address
);