using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Auth;

public record RegisterRequest(
    [Required] string FullName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password,
    string? BusinessName,
    string? Phone
);