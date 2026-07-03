using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Auth;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);