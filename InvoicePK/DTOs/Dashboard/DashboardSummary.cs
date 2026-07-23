using InvoicePK.DTOs.Invoice;

namespace InvoicePK.DTOs.Dashboard;

public record DashboardSummary(
    int TotalInvoices,
    int PaidInvoices,
    int UnpaidInvoices,
    int OverdueInvoices,
    List<CurrencyBreakdown> Breakdown,
    List<InvoiceListItem> RecentInvoices
);