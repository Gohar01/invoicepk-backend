using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Auth;

public record ForgotPasswordRequest(
    [Required, EmailAddress] string Email
);