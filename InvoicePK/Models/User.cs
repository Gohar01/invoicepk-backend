namespace InvoicePK.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? NTN { get; set; }
    public string? LogoUrl { get; set; }
    public string Plan { get; set; } = "Trial";           // Trial | Basic | Pro
    public DateTime? PlanExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    // ── NEW: Password reset fields ──
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }
    // Navigation
    public ICollection<Client> Clients { get; set; } = new List<Client>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}