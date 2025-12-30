using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DenemeTest.Blazor.Controllers
{
    [Route("api/recordings")]
    public class RecordingController : Controller
    {
        private readonly string _root;

        public RecordingController(IConfiguration config, IWebHostEnvironment env)
        {
            // appsettings.json: "Exam": { "RecordingTemp": "App_Data/recordings" }
            var cfg = config.GetSection("Exam:RecordingTemp")?.Value ?? "App_Data/recordings";
            _root = Path.IsPathRooted(cfg) ? cfg : Path.Combine(env.ContentRootPath, cfg);
            Directory.CreateDirectory(_root);
        }

        // POST /api/recordings/finalize-upload?sessionId={guid}&mime=video/webm&kind=cam|screen
        [HttpPost("finalize-upload")]
        public async Task<IActionResult> FinalizeUpload([FromQuery] Guid sessionId, [FromQuery] string? mime, [FromQuery] string kind)
        {
            if (sessionId == Guid.Empty) return BadRequest("sessionId empty");
            kind = (kind ?? "cam").ToLowerInvariant();
            if (kind != "cam" && kind != "screen") return BadRequest("kind must be cam|screen");

            var fileName = $"{sessionId:N}-{kind}.webm";
            var path = Path.Combine(_root, fileName);

            using var fs = System.IO.File.Create(path);
            await Request.Body.CopyToAsync(fs);
            await fs.FlushAsync();

            return Ok(new { path = fileName });
        }

        // GET /api/recordings/download?sessionId=...&kind=cam|screen
        [HttpGet("download")]
        public IActionResult Download([FromQuery] Guid sessionId, [FromQuery] string kind = "cam")
        {
            if (sessionId == Guid.Empty) return BadRequest();
            kind = (kind ?? "cam").ToLowerInvariant();
            var fileName = $"{sessionId:N}-{kind}.webm";
            var path = Path.Combine(_root, fileName);
            if (!System.IO.File.Exists(path)) return NotFound();

            var mime = "video/webm";
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(stream, mime, fileName);
        }
    }
}
