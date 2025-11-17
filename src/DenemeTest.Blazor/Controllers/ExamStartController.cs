using System.Threading.Tasks;
using DenemeTest.Exams;
using Microsoft.AspNetCore.Mvc;

namespace DenemeTest.Controllers;

[Route("exam/start")]
public class ExamStartController : Controller
{
    private readonly IExamRunAppService _examRunApp;

    public ExamStartController(IExamRunAppService examRunApp)
    {
        _examRunApp = examRunApp;
    }

    // GET /exam/start/{token}
    [HttpGet("{token}")]
    public async Task<IActionResult> StartByToken(string token)
    {
        var res = await _examRunApp.StartWithTokenAsync(token);
        // Runner sayfasına gönder
        return Redirect($"/exam/runner/{res.SessionId}");
    }
}
