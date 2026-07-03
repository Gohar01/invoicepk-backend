using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicePK.Data;
using InvoicePK.DTOs.Invoice;
using InvoicePK.DTOs.Client;
using InvoicePK.Helpers;
using InvoicePK.Models;
using InvoicePK.Services;

namespace InvoicePK.Controllers;

[Authorize]
[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InvoiceNumberService _invoiceNumbers;
    private readonly PdfService _pdf;
    private readonly EmailService _email;

    public InvoicesController(
        AppDbContext db,
        InvoiceNumberService invoiceNumbers,
        PdfService pdf,
        EmailService email)
    {
        _db = db;
        _invoiceNumbers = invoiceNumbers;
        _pdf = pdf;
        _email = email;
    }

    // GET /api/invoices?status=Paid
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var userId = User.GetUserId();

        var query = _db.Invoices
            .Include(i => i.Client)
            .Where(i => i.UserId == userId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(i => i.Status == status);

        // Auto-mark overdue
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.Invoices
            .Where(i => i.UserId == userId && i.Status == "Sent" && i.DueDate < today)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "Overdue"));

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceListItem(
                i.Id, i.InvoiceNumber, i.Client.Name,
                i.IssueDate, i.DueDate, i.TotalAmount,
                i.Status, i.CreatedAt))
            .ToListAsync();

        return Ok(invoices);
    }

    // GET /api/invoices/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = User.GetUserId();
        var invoice = await LoadInvoice(id, userId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });
        return Ok(MapToDetail(invoice));
    }

    // GET /api/invoices/{id}/pdf  ← NEW
    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        var userId = User.GetUserId();
        var invoice = await LoadInvoice(id, userId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var pdfBytes = _pdf.GenerateInvoicePdf(invoice, user);

        return File(pdfBytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
    }

    // POST /api/invoices/{id}/send  ← NEW
    [HttpPost("{id}/send")]
    public async Task<IActionResult> SendInvoice(int id)
    {
        var userId = User.GetUserId();
        var invoice = await LoadInvoice(id, userId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });

        if (string.IsNullOrEmpty(invoice.Client.Email))
            return BadRequest(new { message = "Client has no email address." });

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        // Generate PDF
        var pdfBytes = _pdf.GenerateInvoicePdf(invoice, user);

        // Send email
        var sent = await _email.SendInvoiceAsync(invoice, user, pdfBytes);

        if (!sent)
            return StatusCode(500, new { message = "Failed to send email. Check SMTP settings." });

        // Update status to Sent (if still Draft)
        if (invoice.Status == "Draft")
        {
            invoice.Status    = "Sent";
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        // Log the email
        _db.EmailLogs.Add(new EmailLog
        {
            InvoiceId = invoice.Id,
            Type      = "Invoice",
            Status    = "Sent"
        });

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Invoice sent to {invoice.Client.Email}." });
    }

    // POST /api/invoices/{id}/remind  ← NEW
    [HttpPost("{id}/remind")]
    public async Task<IActionResult> SendReminder(int id)
    {
        var userId = User.GetUserId();
        var invoice = await LoadInvoice(id, userId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });

        if (invoice.Status == "Paid")
            return BadRequest(new { message = "Invoice is already paid." });

        if (string.IsNullOrEmpty(invoice.Client.Email))
            return BadRequest(new { message = "Client has no email address." });

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var pdfBytes = _pdf.GenerateInvoicePdf(invoice, user);
        var sent = await _email.SendReminderAsync(invoice, user, pdfBytes);

        if (!sent)
            return StatusCode(500, new { message = "Failed to send reminder. Check SMTP settings." });

        _db.EmailLogs.Add(new EmailLog
        {
            InvoiceId = invoice.Id,
            Type      = "Reminder",
            Status    = "Sent"
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Reminder sent to {invoice.Client.Email}." });
    }

    // POST /api/invoices
    [HttpPost]
    public async Task<IActionResult> Create(CreateInvoiceRequest req)
    {
        var userId = User.GetUserId();

        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.Id == req.ClientId && c.UserId == userId);
        if (client == null)
            return BadRequest(new { message = "Client not found." });

        var subTotal  = req.Items.Sum(i => i.Quantity * i.UnitPrice);
        var gstAmount = Math.Round(subTotal * (req.GSTPercent / 100), 2);
        var total     = subTotal + gstAmount;

        var invoice = new Invoice
        {
            UserId        = userId,
            ClientId      = req.ClientId,
            InvoiceNumber = await _invoiceNumbers.GenerateAsync(userId),
            IssueDate     = req.IssueDate,
            DueDate       = req.DueDate,
            GSTPercent    = req.GSTPercent,
            SubTotal      = subTotal,
            GSTAmount     = gstAmount,
            TotalAmount   = total,
            Notes         = req.Notes,
            Status        = "Draft",
            Items         = req.Items.Select(i => new InvoiceItem
            {
                Description = i.Description,
                Quantity    = i.Quantity,
                UnitPrice   = i.UnitPrice
            }).ToList()
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        await _db.Entry(invoice).Reference(i => i.Client).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, MapToDetail(invoice));
    }

    // PUT /api/invoices/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateInvoiceRequest req)
    {
        var userId = User.GetUserId();
        var invoice = await _db.Invoices.Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (invoice == null) return NotFound(new { message = "Invoice not found." });
        if (invoice.Status == "Paid")
            return BadRequest(new { message = "Cannot edit a paid invoice." });

        if (req.ClientId   != null) invoice.ClientId   = req.ClientId.Value;
        if (req.IssueDate  != null) invoice.IssueDate  = req.IssueDate.Value;
        if (req.DueDate    != null) invoice.DueDate    = req.DueDate.Value;
        if (req.GSTPercent != null) invoice.GSTPercent = req.GSTPercent.Value;
        if (req.Notes      != null) invoice.Notes      = req.Notes;

        if (req.Items != null && req.Items.Any())
        {
            _db.InvoiceItems.RemoveRange(invoice.Items);
            invoice.Items = req.Items.Select(i => new InvoiceItem
            {
                Description = i.Description,
                Quantity    = i.Quantity,
                UnitPrice   = i.UnitPrice
            }).ToList();

            invoice.SubTotal    = invoice.Items.Sum(i => i.Quantity * i.UnitPrice);
            invoice.GSTAmount   = Math.Round(invoice.SubTotal * (invoice.GSTPercent / 100), 2);
            invoice.TotalAmount = invoice.SubTotal + invoice.GSTAmount;
        }

        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Invoice updated." });
    }

    // PUT /api/invoices/{id}/status
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateStatusRequest req)
    {
        var userId = User.GetUserId();
        var validStatuses = new[] { "Draft", "Sent", "Paid", "Overdue" };
        if (!validStatuses.Contains(req.Status))
            return BadRequest(new { message = "Invalid status." });

        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });

        invoice.Status    = req.Status;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = $"Invoice marked as {req.Status}." });
    }

    // DELETE /api/invoices/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.GetUserId();
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

        if (invoice == null) return NotFound(new { message = "Invoice not found." });
        if (invoice.Status == "Paid")
            return BadRequest(new { message = "Cannot delete a paid invoice." });

        _db.Invoices.Remove(invoice);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Invoice deleted." });
    }

    // ── Helpers ───────────────────────────────────
    private async Task<Invoice?> LoadInvoice(int id, int userId) =>
        await _db.Invoices
            .Include(i => i.Client)
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

    private static InvoiceDetailResponse MapToDetail(Invoice i) => new(
        i.Id, i.InvoiceNumber,
        new ClientResponse(i.Client.Id, i.Client.Name, i.Client.Email,
            i.Client.Phone, i.Client.Address, i.Client.CreatedAt, 0),
        i.IssueDate, i.DueDate, i.GSTPercent,
        i.SubTotal, i.GSTAmount, i.TotalAmount,
        i.Status, i.Notes,
        i.Items.Select(item => new InvoiceItemResponse(
            item.Id, item.Description, item.Quantity,
            item.UnitPrice, item.Quantity * item.UnitPrice)).ToList(),
        i.CreatedAt
    );
}
