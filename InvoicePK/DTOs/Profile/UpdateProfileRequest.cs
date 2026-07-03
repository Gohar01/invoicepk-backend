namespace InvoicePK.DTOs.Profile;
public record UpdateProfileRequest(
    string? FullName,
    string? BusinessName,
    string? Phone,
    string? Address,
    string? NTN
);