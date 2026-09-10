using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Auth;

public record RegisterRequest(
    [Required, MaxLength(100)] string FullName,
    [Required, EmailAddress, MaxLength(100)] string Email,
    [Required, MinLength(6), MaxLength(100)] string Password,
    [MaxLength(100)] string? BusinessName,
    [RegularExpression(@"^((\+92|92|0)?[- ]?[2-9]\d{1,3}[- ]?\d{6,8}|\+(?!92)[1-9]\d{0,2}[- ]?[\d\s\-()]{6,14})([, /]+((\+92|92|0)?[- ]?[2-9]\d{1,3}[- ]?\d{6,8}|\+(?!92)[1-9]\d{0,2}[- ]?[\d\s\-()]{6,14}))*$", ErrorMessage = "Please enter valid phone number(s) (e.g. 0300-1234567 or 021-34567890)."), MaxLength(100)] string? Phone
);