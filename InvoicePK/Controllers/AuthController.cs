using InvoicePK.Data;
using InvoicePK.DTOs;
using InvoicePK.DTOs.Auth;
using InvoicePK.DTOs.Profile;
using InvoicePK.Helpers;
using InvoicePK.Models;
using InvoicePK.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoicePK.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtHelper _jwt;
    private readonly EmailService _email;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, JwtHelper jwt, EmailService email, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _email = email;
        _config = config;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        // Check email already exists
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return BadRequest(new { message = "Email already registered." });

        var user = new User
        {
            FullName     = req.FullName,
            Email        = req.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            BusinessName = req.BusinessName,
            Phone        = req.Phone,
            Plan         = "Trial",
            PlanExpiresAt = DateTime.UtcNow.AddDays(14)  // 14-day free trial
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user);

        return Ok(new AuthResponse(token, user.FullName, user.Email, user.Plan));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == req.Email.ToLower());

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var token = _jwt.GenerateToken(user);

        return Ok(new AuthResponse(token, user.FullName, user.Email, user.Plan));
    }

    [Authorize]
    [HttpGet("profile")]    
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user == null)
            return NotFound(new { message = "User not found." });

        return Ok(new ProfileResponse(
            user.Id, user.FullName, user.Email,
            user.BusinessName, user.Phone, user.Address,
            user.NTN, user.LogoUrl, user.Plan, user.PlanExpiresAt
        ));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest req)
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user == null) return NotFound();

        if (req.FullName != null)     user.FullName     = req.FullName;
        if (req.BusinessName != null) user.BusinessName = req.BusinessName;
        if (req.Phone != null)        user.Phone        = req.Phone;
        if (req.Address != null)      user.Address      = req.Address;
        if (req.NTN != null)          user.NTN          = req.NTN;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Profile updated." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == req.Email.ToLower());

        // Always return success even if email not found (security best practice —
        // don't reveal which emails are registered)
        if (user == null)
            return Ok(new { message = "If that email exists, a reset link has been sent." });

        // Generate a secure random token
        user.ResetToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        user.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1); // expires in 1 hour
        await _db.SaveChangesAsync();

        // Build reset link — update FRONTEND_URL in appsettings.json
        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:5173";
        var resetLink = $"{frontendUrl}/reset-password?token={user.ResetToken}";

        // Send email
        await _email.SendPasswordResetAsync(user, resetLink);

        return Ok(new { message = "If that email exists, a reset link has been sent." });
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.ResetToken == req.Token);

        if (user == null || user.ResetTokenExpiresAt == null || user.ResetTokenExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "This reset link is invalid or has expired." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.ResetToken = null;
        user.ResetTokenExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password reset successfully. You can now log in." });
    }

    // POST /api/auth/admin-reset-password
    [HttpPost("admin-reset-password")]
    public async Task<IActionResult> AdminResetPassword([FromBody] AdminResetRequest req)
    {
        // Simple secret key check — set this in Railway env vars as AdminSecret
        var adminSecret = _config["AdminSecret"];
        if (string.IsNullOrEmpty(adminSecret) || req.AdminSecret != adminSecret)
            return Unauthorized(new { message = "Invalid admin secret." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == req.Email.ToLower());
        if (user == null)
            return NotFound(new { message = "No user found with that email." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Password reset for {user.Email}. Share the new password with them securely." });
    }

    // POST /api/auth/logo
    [Authorize]
    [HttpPost("logo")]
    [RequestSizeLimit(2 * 1024 * 1024)] // 2MB max
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { message = "Only PNG, JPG, or WEBP images are allowed." });

        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(new { message = "Logo must be smaller than 2MB." });

        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        // Convert to base64 data URI so it can be stored directly in the DB
        // and used directly as an <img src="..."> or in the PDF generator.
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        user.LogoUrl = $"data:{file.ContentType};base64,{base64}";
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { logoUrl = user.LogoUrl });
    }

    // DELETE /api/auth/logo
    [Authorize]
    [HttpDelete("logo")]
    public async Task<IActionResult> DeleteLogo()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.LogoUrl = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Logo removed." });
    }

}