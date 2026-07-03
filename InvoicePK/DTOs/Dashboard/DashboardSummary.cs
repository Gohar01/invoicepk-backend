using InvoicePK.DTOs.Invoice;

namespace InvoicePK.DTOs.Dashboard;

public record DashboardSummary(
    int TotalInvoices,
    int PaidInvoices,
    int UnpaidInvoices,
    int OverdueInvoices,
    decimal TotalRevenue,
    decimal PendingAmount,
    List<InvoiceListItem> RecentInvoices
);