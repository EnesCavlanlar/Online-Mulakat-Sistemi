using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DenemeTest.Application.Exams; // IExamSessionAppService buradan geliyor

namespace DenemeTest.Controllers
{
    [Route("api/exam/start")]
    public class ExamStartController : Controller
    {
        private readonly IExamSessionAppService _sessionApp;

        public ExamStartController(IExamSessionAppService sessionApp)
        {
            _sessionApp = sessionApp;
        }

        // GET /api/exam/start/{token}
        [HttpGet("{token}")]
        public async Task<IActionResult> StartByToken(string token)
        {
            // mevcut servisi kullanıyoruz
            var result = await _sessionApp.StartByTokenAsync(token);

            // result.Id = ExamSession Id
            return Redirect($"/exam/runner/{result.Id}");
        }
    }
}
