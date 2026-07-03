using InvoicePK.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoicePK.Services;

public class InvoiceNumberService
{
    private readonly AppDbContext _db;

    public InvoiceNumberService(AppDbContext db) => _db = db;

    // Generates INV-0001, INV-0002, etc. per user
    public async Task<string> GenerateAsync(int userId)
    {
        var last = await _db.Invoices
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null)
        {
            var parts = last.InvoiceNumber.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var n))
                next = n + 1;
        }

        return $"INV-{next:D4}";
    }
}