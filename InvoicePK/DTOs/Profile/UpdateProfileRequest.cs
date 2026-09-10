using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Profile;

public record UpdateProfileRequest(
    [MaxLength(100)] string? FullName,
    [MaxLength(100)] string? BusinessName,
    [RegularExpression(@"^((\+92|92|0)?[- ]?[2-9]\d{1,3}[- ]?\d{6,8}|\+(?!92)[1-9]\d{0,2}[- ]?[\d\s\-()]{6,14})([, /]+((\+92|92|0)?[- ]?[2-9]\d{1,3}[- ]?\d{6,8}|\+(?!92)[1-9]\d{0,2}[- ]?[\d\s\-()]{6,14}))*$", ErrorMessage = "Please enter valid phone number(s) (e.g. 0300-1234567 or 021-34567890)."), MaxLength(100)] string? Phone,
    [MaxLength(250)] string? Address,
    [RegularExpression(@"^(\d{7}-?\d|\d{8})?$", ErrorMessage = "NTN must be in format 1234567-8 or 8 digits."), MaxLength(20)] string? NTN
);