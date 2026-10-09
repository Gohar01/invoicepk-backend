namespace InvoicePK.DTOs.Auth;

public class GoogleAuthRequest
{
    public string? Credential { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? BusinessName { get; set; }
    public string? GoogleId { get; set; }
}
