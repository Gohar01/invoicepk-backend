namespace InvoicePK.DTOs.Profile;

public record ProfileResponse(
    int Id,
    string FullName,
    string Email,
    string? BusinessName,
    string? Phone,
    string? Address,
    string? NTN,
    string? LogoUrl,
    string Plan,
    DateTime? PlanExpiresAt
);