using Microsoft.AspNetCore.Mvc;

namespace DenemeTest.Blazor.Controllers;

[Route("api/policy")]
[ApiController]
public class ExamPolicyController : ControllerBase
{
    // Gerekirse ilerde server-side ek kontroller eklenir (aktif session var mı vs.)
    // Şimdilik placeholder; client tarafı iptal çağrılarını doğrudan AppService ile yapıyor.
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { ok = true });
}
