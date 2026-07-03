using InvoicePK.Data;
using InvoicePK.Helpers;
using InvoicePK.DTOs.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using InvoicePK.Models;

namespace InvoicePK.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ClientsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = User.GetUserId();

        var clients = await _db.Clients
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ClientResponse(
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.Address,
                c.CreatedAt,
                c.Invoices.Count
            ))
            .ToListAsync();

        return Ok(clients);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = User.GetUserId();

        var client = await _db.Clients
            .Where(c => c.Id == id && c.UserId == userId)
            .Select(c => new ClientResponse(
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.Address,
                c.CreatedAt,
                c.Invoices.Count
            ))
            .FirstOrDefaultAsync();

        if (client == null) return NotFound(new { message = "Client not found." });

        return Ok(client);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateClientRequest req)
    {
        var userId = User.GetUserId();

        var client = new Client
        {
            UserId  = userId,
            Name    = req.Name,
            Email   = req.Email,
            Phone   = req.Phone,
            Address = req.Address
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = client.Id },
            new ClientResponse(client.Id, client.Name, client.Email,
                client.Phone, client.Address, client.CreatedAt, 0));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateClientRequest req)
    {
        var userId = User.GetUserId();
        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (client == null) return NotFound(new { message = "Client not found." });

        if (req.Name    != null) client.Name    = req.Name;
        if (req.Email   != null) client.Email   = req.Email;
        if (req.Phone   != null) client.Phone   = req.Phone;
        if (req.Address != null) client.Address = req.Address;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Client updated." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.GetUserId();
        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (client == null) return NotFound(new { message = "Client not found." });

        // Check if client has invoices
        var hasInvoices = await _db.Invoices.AnyAsync(i => i.ClientId == id);
        if (hasInvoices)
            return BadRequest(new { message = "Cannot delete client with existing invoices." });

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Client deleted." });
    }
}