using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicePK.Data;
using InvoicePK.DTOs.Dashboard;
using InvoicePK.DTOs.Invoice;
using InvoicePK.Helpers;

namespace InvoicePK.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db) => _db = db;

    // GET /api/dashboard/summary
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var userId = User.GetUserId();

        var invoices = await _db.Invoices
            .Include(i => i.Client)
            .Where(i => i.UserId == userId)
            .ToListAsync();

        // Group revenue/pending amounts PER CURRENCY — summing across different
        // currencies (e.g. PKR + USD) as one number would be meaningless.
        var breakdown = invoices
            .GroupBy(i => i.Currency)
            .Select(g => new CurrencyBreakdown(
                Currency:      g.Key,
                TotalRevenue:  g.Where(i => i.Status == "Paid").Sum(i => i.TotalAmount),
                PendingAmount: g.Where(i => i.Status is "Sent" or "Overdue").Sum(i => i.TotalAmount),
                InvoiceCount:  g.Count()
            ))
            .OrderByDescending(b => b.InvoiceCount)
            .ToList();

        var summary = new DashboardSummary(
            TotalInvoices:   invoices.Count,
            PaidInvoices:    invoices.Count(i => i.Status == "Paid"),
            UnpaidInvoices:  invoices.Count(i => i.Status == "Sent" || i.Status == "Draft"),
            OverdueInvoices: invoices.Count(i => i.Status == "Overdue"),
            Breakdown:       breakdown,
            RecentInvoices:  invoices
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .Select(i => new InvoiceListItem(
                    i.Id, i.InvoiceNumber, i.Client.Name, i.Currency,
                    i.IssueDate, i.DueDate, i.TotalAmount,
                    i.Status, i.CreatedAt))
                .ToList()
        );

        return Ok(summary);
    }
}
