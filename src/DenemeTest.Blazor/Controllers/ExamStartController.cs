using Microsoft.AspNetCore.Mvc;

namespace DenemeTest.Controllers
{
    [Route("api/exam/start")]
    public class ExamStartController : Controller
    {
        // GET /api/exam/start/{token}
        // Eskiden: doğrudan /exam/runner/{sessionId} adresine yönlendiriyordu,
        // bu da kamera/mikrofon izin ekranını tamamen baypas ediyordu.
        //
        // Artık her zaman Blazor pre-exam sayfasına gidiyoruz:
        //   /exam/start/{token}
        // Böylece aday önce kamera+mikrofon izni veriyor, sonra soruları görüyor.
        [HttpGet("{token}")]
        public IActionResult StartByToken(string token)
        {
            return Redirect($"/exam/start/{token}");
        }
    }
}
