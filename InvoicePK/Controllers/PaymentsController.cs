using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicePK.Data;
using InvoicePK.DTOs.Payments;
using InvoicePK.Helpers;
using InvoicePK.Models;
using InvoicePK.Services;

namespace InvoicePK.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly IConfiguration _config;

    public PaymentsController(AppDbContext db, EmailService email, IConfiguration config)
    {
        _db = db;
        _email = email;
        _config = config;
    }

    // POST /api/payments/proof (Public: can be submitted by authenticated user or guest)
    [HttpPost("proof")]
    public async Task<IActionResult> SubmitProof([FromBody] SubmitPaymentProofRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TransactionId))
            return BadRequest(new { message = "Transaction ID / Reference Number is required." });

        if (string.IsNullOrWhiteSpace(req.SenderName))
            return BadRequest(new { message = "Business name or account email is required." });

        var trxId = req.TransactionId.Trim();

        // Check if transaction ID was already submitted
        var existing = await _db.PaymentProofs.FirstOrDefaultAsync(p => p.TransactionId == trxId);
        if (existing != null)
        {
            return Ok(new
            {
                message = "This transaction ID has already been recorded and is currently being processed.",
                status = existing.Status,
                proofId = existing.Id
            });
        }

        // Determine user identity if available
        int? userId = null;
        string? email = req.AccountEmail?.Trim().ToLowerInvariant();

        if (User.Identity?.IsAuthenticated == true)
        {
            var authUserId = User.GetUserId();
            if (authUserId > 0)
            {
                userId = authUserId;
                var authUser = await _db.Users.FindAsync(authUserId);
                if (authUser != null && string.IsNullOrEmpty(email))
                {
                    email = authUser.Email;
                }
            }
        }

        // If not authenticated, check if the sender provided an email that matches an existing account
        if (userId == null && !string.IsNullOrEmpty(email))
        {
            var matchedUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (matchedUser != null)
            {
                userId = matchedUser.Id;
            }
        }
        else if (userId == null && req.SenderName.Contains('@'))
        {
            var senderEmail = req.SenderName.Trim().ToLowerInvariant();
            var matchedUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == senderEmail);
            if (matchedUser != null)
            {
                userId = matchedUser.Id;
                email = matchedUser.Email;
            }
        }

        var proof = new PaymentProof
        {
            UserId        = userId,
            SenderName    = req.SenderName.Trim(),
            SenderPhone   = req.SenderPhone?.Trim(),
            AccountEmail  = email,
            TransactionId = trxId,
            Plan          = !string.IsNullOrWhiteSpace(req.Plan) ? req.Plan.Trim() : "Lifetime Pro",
            Amount        = req.Amount > 0 ? req.Amount : (req.Plan?.Contains("Month", StringComparison.OrdinalIgnoreCase) == true ? 999 : 2999),
            Status        = "Pending",
            CreatedAt     = DateTime.UtcNow
        };

        _db.PaymentProofs.Add(proof);
        await _db.SaveChangesAsync();

        // Fire alert email to admin
        try
        {
            await _email.SendPaymentProofAlertAsync(proof);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Payment proof alert email notification failed: {ex.Message}");
        }

        return Ok(new
        {
            message = "Payment proof received successfully! Our team is verifying your transaction.",
            proofId = proof.Id,
            status = proof.Status
        });
    }

    // GET /api/payments/proofs (Admin only)
    [HttpGet("proofs")]
    public async Task<IActionResult> GetProofs([FromQuery] string? secret)
    {
        var adminSecret = _config["AdminSecret"];
        var headerSecret = Request.Headers["X-Admin-Secret"].FirstOrDefault();

        if (string.IsNullOrEmpty(adminSecret) || (secret != adminSecret && headerSecret != adminSecret))
            return Unauthorized(new { message = "Invalid admin secret." });

        var proofs = await _db.PaymentProofs
            .Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new
            {
                p.Id,
                p.UserId,
                UserFullName = p.User != null ? p.User.FullName : null,
                UserEmail = p.User != null ? p.User.Email : p.AccountEmail,
                p.SenderName,
                p.SenderPhone,
                p.TransactionId,
                p.Plan,
                p.Amount,
                p.Status,
                p.AdminNotes,
                p.CreatedAt,
                p.ProcessedAt
            })
            .ToListAsync();

        return Ok(proofs);
    }

    // POST /api/payments/approve/{id} (Admin only)
    [HttpPost("approve/{id}")]
    public async Task<IActionResult> ApproveProof(int id, [FromBody] ApprovePaymentProofRequest req)
    {
        var adminSecret = _config["AdminSecret"];
        if (string.IsNullOrEmpty(adminSecret) || req.AdminSecret != adminSecret)
            return Unauthorized(new { message = "Invalid admin secret." });

        var proof = await _db.PaymentProofs.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        if (proof == null)
            return NotFound(new { message = "Payment proof not found." });

        proof.Status = "Approved";
        proof.AdminNotes = req.AdminNotes;
        proof.ProcessedAt = DateTime.UtcNow;

        // Upgrade user plan
        var user = proof.User;
        if (user == null && !string.IsNullOrEmpty(proof.AccountEmail))
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == proof.AccountEmail.ToLowerInvariant());
        }

        if (user != null)
        {
            user.Plan = "Pro";
            user.PlanExpiresAt = proof.Plan.Contains("Lifetime", StringComparison.OrdinalIgnoreCase)
                ? DateTime.UtcNow.AddYears(100)
                : DateTime.UtcNow.AddMonths(1);
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = $"Proof #{proof.Id} approved successfully.",
            userUpgraded = user != null,
            userEmail = user?.Email,
            plan = user?.Plan,
            planExpiresAt = user?.PlanExpiresAt
        });
    }
}
