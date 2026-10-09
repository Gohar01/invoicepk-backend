using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicePK.Data;
using InvoicePK.Helpers;
using InvoicePK.Models;
using InvoicePK.Services;
using InvoicePK.DTOs.Auth;

namespace InvoicePK.Controllers;

// Purely authentication: register, login, forgot/reset password.
// Everything about "your profile / your logo" now lives in ProfileController.
[ApiController]
[Route("api/auth")]
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
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return BadRequest(new { message = "Email already registered." });

        var user = new User
        {
            FullName      = req.FullName,
            Email         = req.Email.ToLower(),
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword(req.Password),
            BusinessName  = req.BusinessName,
            Phone         = req.Phone,
            Plan          = "Trial",
            PlanExpiresAt = DateTime.UtcNow.AddDays(14)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponse(token, user.FullName, user.Email, user.Plan));
    }

    // POST /api/auth/google
    [HttpPost("google")]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthRequest req)
    {
        string? email = req.Email;
        string? name = req.FullName;

        // If a JWT credential was supplied from Google GIS, decode payload
        if (!string.IsNullOrEmpty(req.Credential))
        {
            try
            {
                var parts = req.Credential.Split('.');
                if (parts.Length >= 2)
                {
                    var payloadJson = System.Text.Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                    using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("email", out var emailProp))
                        email = emailProp.GetString();
                    if (root.TryGetProperty("name", out var nameProp))
                        name = nameProp.GetString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google token parse warning: {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email is required for Google Sign-In." });

        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User
            {
                FullName      = !string.IsNullOrWhiteSpace(name) ? name.Trim() : "Google User",
                Email         = email,
                PasswordHash  = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                BusinessName  = !string.IsNullOrWhiteSpace(req.BusinessName) ? req.BusinessName.Trim() : (!string.IsNullOrWhiteSpace(name) ? $"{name.Trim()}'s Business" : "My Business"),
                Plan          = "Trial",
                PlanExpiresAt = DateTime.UtcNow.AddDays(14)
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponse(token, user.FullName, user.Email, user.Plan));
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input;
        output = output.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponse(token, user.FullName, user.Email, user.Plan));
    }

    // POST /api/auth/forgot-password
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());

        if (user == null)
            return Ok(new { message = "If that email exists, a reset link has been sent." });

        user.ResetToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        user.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:5173";
        var resetLink = $"{frontendUrl}/reset-password?token={user.ResetToken}";
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
        var adminSecret = _config["AdminSecret"];
        if (string.IsNullOrEmpty(adminSecret) || req.AdminSecret != adminSecret)
            return Unauthorized(new { message = "Invalid admin secret." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());
        if (user == null)
            return NotFound(new { message = "No user found with that email." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Password reset for {user.Email}. Share the new password with them securely." });
    }
}
