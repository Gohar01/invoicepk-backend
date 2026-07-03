using InvoicePK.Data;
using InvoicePK.DTOs;
using InvoicePK.DTOs.Auth;
using InvoicePK.DTOs.Profile;
using InvoicePK.Helpers;
using InvoicePK.Models;
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

    public AuthController(AppDbContext db, JwtHelper jwt)
    {
        _db = db;
        _jwt = jwt;
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
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());

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
}