namespace InvoicePK.DTOs.Auth;

public record AuthResponse(
    string Token,
    string FullName,
    string Email,
    string Plan
);