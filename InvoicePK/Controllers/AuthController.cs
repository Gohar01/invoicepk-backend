using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth;
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
        if (string.IsNullOrWhiteSpace(req.Credential))
        {
            return BadRequest(new { message = "Google credential token is required." });
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            var clientId = _config["Google:ClientId"] ?? _config["Authentication:Google:ClientId"];
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                settings.Audience = new[] { clientId };
            }

            payload = await GoogleJsonWebSignature.ValidateAsync(req.Credential, settings);
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { message = "Invalid Google token signature or expired token." });
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = $"Google token validation failed: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            return BadRequest(new { message = "Email not found in Google credential token." });
        }

        if (!payload.EmailVerified)
        {
            return Unauthorized(new { message = "Google email address is not verified." });
        }

        var email = payload.Email.Trim().ToLowerInvariant();
        var name = !string.IsNullOrWhiteSpace(payload.Name) 
            ? payload.Name.Trim() 
            : (!string.IsNullOrWhiteSpace(payload.GivenName) ? payload.GivenName.Trim() : "Google User");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User
            {
                FullName      = name,
                Email         = email,
                PasswordHash  = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                BusinessName  = !string.IsNullOrWhiteSpace(req.BusinessName) ? req.BusinessName.Trim() : $"{name}'s Business",
                Plan          = "Trial",
                PlanExpiresAt = DateTime.UtcNow.AddDays(14)
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponse(token, user.FullName, user.Email, user.Plan));
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
