using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Auth;

public record ResetPasswordRequest(
    [Required] string Token,
    [Required, MinLength(6)] string NewPassword
);