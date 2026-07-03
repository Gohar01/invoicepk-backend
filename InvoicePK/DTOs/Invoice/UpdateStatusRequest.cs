using System.ComponentModel.DataAnnotations;

namespace InvoicePK.DTOs.Invoice;

public record UpdateStatusRequest(
    [Required] string Status  // Draft | Sent | Paid | Overdue
);