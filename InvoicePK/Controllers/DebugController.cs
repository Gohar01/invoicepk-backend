// ── TEMPORARY — add this controller to diagnose the Railway env var issue ──
// DELETE this file once the problem is fixed. It exposes partial config info
// which shouldn't stay in a production app long-term.

using Microsoft.AspNetCore.Mvc;

namespace InvoicePK.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly IConfiguration _config;
    public DebugController(IConfiguration config) => _config = config;

    // GET /api/debug/resend-key
    [HttpGet("resend-key")]
    public IActionResult CheckResendKey()
    {
        var key = _config["Resend:ApiKey"];
        var fromEmail = _config["Resend:FromEmail"];
        var environment = _config["ASPNETCORE_ENVIRONMENT"] ??
                           Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return Ok(new
        {
            environment,
            keyIsNull = key == null,
            keyLength = key?.Length ?? 0,
            keyFirst6 = key != null && key.Length >= 6 ? key.Substring(0, 6) : null,
            keyLast4  = key != null && key.Length >= 4 ? key.Substring(key.Length - 4) : null,
            fromEmail
        });
    }
}
