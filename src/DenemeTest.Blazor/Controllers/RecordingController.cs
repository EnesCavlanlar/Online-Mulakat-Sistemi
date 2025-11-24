using DenemeTest.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;

namespace DenemeTest.Blazor.Controllers
{
    [Route("api/recordings")]
    [ApiController]
    [IgnoreAntiforgeryToken]
    public class RecordingController : ControllerBase
    {
        private readonly IBlobContainer<ExamRecordingContainer> _recordings;
        private readonly IConfiguration _config;

        public RecordingController(
            IBlobContainer<ExamRecordingContainer> recordings,
            IConfiguration config)
        {
            _recordings = recordings;
            _config = config;
        }

        private static string GetBlobName(Guid sessionId) => $"recordings/{sessionId}.webm";

        // YENİ: Tek seferde tam webm dosyasını alır ve blob'a kaydeder.
        // POST /api/recordings/finalize-upload?sessionId=...&mime=video/webm
        [HttpPost("finalize-upload")]
        public async Task<IActionResult> FinalizeUpload([FromQuery] Guid sessionId, [FromQuery] string? mime = null)
        {
            if (sessionId == Guid.Empty)
                return BadRequest("sessionId zorunludur.");

            // Gövde yoksa hata
            if (Request.Body == null)
                return BadRequest("Boş içerik.");

            // Body akışını doğrudan blob'a kopyala
            await _recordings.SaveAsync(GetBlobName(sessionId), Request.Body, overrideExisting: true);

            return Ok(new { ok = true });
        }

        // İNDİRME – admin panelinde kullanılacak
        // GET /api/recordings/download?sessionId=...
        [HttpGet("download")]
        public async Task<IActionResult> Download([FromQuery] Guid sessionId)
        {
            if (sessionId == Guid.Empty)
                return BadRequest("sessionId zorunludur.");

            var name = GetBlobName(sessionId);
            if (!await _recordings.ExistsAsync(name))
                return NotFound();

            var stream = await _recordings.GetAsync(name);
            return File(stream, "video/webm", $"{sessionId}.webm");
        }
    }
}
