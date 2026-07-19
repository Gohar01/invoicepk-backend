using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using InvoicePK.Data;
using InvoicePK.DTOs.Profile;
using InvoicePK.Helpers;

namespace InvoicePK.Controllers;

// Everything about "who am I / how do I present myself on invoices" lives here.
// AuthController stays focused purely on register/login/tokens/password reset.
[Authorize]
[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db) => _db = db;

    // GET /api/profile
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new ProfileResponse(
            user.Id, user.FullName, user.Email,
            user.BusinessName, user.Phone, user.Address,
            user.NTN, user.LogoUrl, user.Plan, user.PlanExpiresAt
        ));
    }

    // PUT /api/profile
    [HttpPut]
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

    // POST /api/profile/logo
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

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        user.LogoUrl = $"data:{file.ContentType};base64,{base64}";
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { logoUrl = user.LogoUrl });
    }

    // DELETE /api/profile/logo
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
