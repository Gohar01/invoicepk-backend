using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Auth;

public record AdminResetRequest(
    [Required] string AdminSecret,
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string NewPassword
);