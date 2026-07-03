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

        var summary = new DashboardSummary(
            TotalInvoices:   invoices.Count,
            PaidInvoices:    invoices.Count(i => i.Status == "Paid"),
            UnpaidInvoices:  invoices.Count(i => i.Status == "Sent" || i.Status == "Draft"),
            OverdueInvoices: invoices.Count(i => i.Status == "Overdue"),
            TotalRevenue:    invoices.Where(i => i.Status == "Paid").Sum(i => i.TotalAmount),
            PendingAmount:   invoices.Where(i => i.Status is "Sent" or "Overdue").Sum(i => i.TotalAmount),
            RecentInvoices:  invoices
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .Select(i => new InvoiceListItem(
                    i.Id, i.InvoiceNumber, i.Client.Name,
                    i.IssueDate, i.DueDate, i.TotalAmount,
                    i.Status, i.CreatedAt))
                .ToList()
        );

        return Ok(summary);
    }
}
