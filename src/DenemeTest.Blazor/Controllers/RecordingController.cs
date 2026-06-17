using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DenemeTest.Blazor.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/recordings")]
    public class RecordingController : ControllerBase
    {
        private readonly string _root;

        public RecordingController(IConfiguration config, IWebHostEnvironment env)
        {
            var cfg = config.GetSection("Exam:RecordingTemp")?.Value;

            if (string.IsNullOrWhiteSpace(cfg))
            {
                cfg = "App_Data/recordings";
            }

            _root = Path.IsPathRooted(cfg)
                ? cfg
                : Path.Combine(env.ContentRootPath, cfg);

            Directory.CreateDirectory(_root);
        }

        // POST /api/recordings/finalize-upload?sessionId={guid}&mime=video/webm&kind=cam|screen
        [HttpPost("finalize-upload")]
        [DisableRequestSizeLimit]
        [RequestSizeLimit(1024L * 1024L * 1024L)]
        public async Task<IActionResult> FinalizeUpload(
            [FromQuery] Guid sessionId,
            [FromQuery] string? mime,
            [FromQuery] string? kind)
        {
            if (sessionId == Guid.Empty)
            {
                return BadRequest("sessionId boş olamaz.");
            }

            var normalizedKind = NormalizeKind(kind);

            if (normalizedKind == null)
            {
                return BadRequest("kind sadece cam veya screen olabilir.");
            }

            if (Request.Body == null)
            {
                return BadRequest("Kayıt verisi bulunamadı.");
            }

            Directory.CreateDirectory(_root);

            var safeMime = string.IsNullOrWhiteSpace(mime)
                ? "video/webm"
                : mime.Trim();

            var fileName = $"{sessionId:N}-{normalizedKind}.webm";
            var tempFileName = $"{sessionId:N}-{normalizedKind}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.tmp";

            var tempPath = Path.Combine(_root, tempFileName);
            var finalPath = Path.Combine(_root, fileName);

            long writtenBytes = 0;

            try
            {
                await using (var fs = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 1024,
                    useAsync: true))
                {
                    await Request.Body.CopyToAsync(fs);
                    await fs.FlushAsync();
                }

                var tempInfo = new FileInfo(tempPath);
                writtenBytes = tempInfo.Exists ? tempInfo.Length : 0;

                if (writtenBytes <= 0)
                {
                    SafeDelete(tempPath);
                    return BadRequest("Kayıt dosyası boş geldi.");
                }

                if (System.IO.File.Exists(finalPath))
                {
                    System.IO.File.Delete(finalPath);
                }

                System.IO.File.Move(tempPath, finalPath);

                var finalInfo = new FileInfo(finalPath);

                return Ok(new
                {
                    ok = true,
                    sessionId,
                    kind = normalizedKind,
                    fileName,
                    size = finalInfo.Length,
                    mime = safeMime,
                    root = _root,
                    savedPath = finalPath
                });
            }
            catch (Exception ex)
            {
                SafeDelete(tempPath);

                return StatusCode(500, new
                {
                    ok = false,
                    sessionId,
                    kind = normalizedKind,
                    root = _root,
                    writtenBytes,
                    error = ex.Message
                });
            }
        }

        // GET /api/recordings/download?sessionId={guid}&kind=cam|screen
        [HttpGet("download")]
        public IActionResult Download(
            [FromQuery] Guid sessionId,
            [FromQuery] string? kind = "cam")
        {
            if (sessionId == Guid.Empty)
            {
                return BadRequest("sessionId boş olamaz.");
            }

            var normalizedKind = NormalizeKind(kind);

            if (normalizedKind == null)
            {
                return BadRequest("kind sadece cam veya screen olabilir.");
            }

            var fileName = $"{sessionId:N}-{normalizedKind}.webm";
            var path = Path.Combine(_root, fileName);

            if (!System.IO.File.Exists(path))
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Kayıt dosyası bulunamadı.",
                    sessionId,
                    kind = normalizedKind,
                    expectedFileName = fileName,
                    expectedPath = path,
                    root = _root,
                    existingFiles = GetExistingFileNames()
                });
            }

            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";

            return File(stream, "video/webm", fileName);
        }

        // GET /api/recordings/exists?sessionId={guid}
        [HttpGet("exists")]
        public IActionResult Exists([FromQuery] Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                return BadRequest("sessionId boş olamaz.");
            }

            var camFileName = $"{sessionId:N}-cam.webm";
            var screenFileName = $"{sessionId:N}-screen.webm";

            var camPath = Path.Combine(_root, camFileName);
            var screenPath = Path.Combine(_root, screenFileName);

            var camInfo = new FileInfo(camPath);
            var screenInfo = new FileInfo(screenPath);

            var camExists = camInfo.Exists && camInfo.Length > 0;
            var screenExists = screenInfo.Exists && screenInfo.Length > 0;

            return Ok(new
            {
                ok = true,
                sessionId,
                root = _root,

                camExists,
                camFileName = camExists ? camFileName : null,
                camSize = camExists ? camInfo.Length : 0,

                screenExists,
                screenFileName = screenExists ? screenFileName : null,
                screenSize = screenExists ? screenInfo.Length : 0
            });
        }

        // GET /api/recordings/debug
        [HttpGet("debug")]
        public IActionResult Debug()
        {
            Directory.CreateDirectory(_root);

            var files = new DirectoryInfo(_root)
                .GetFiles("*.webm")
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .Take(50)
                .Select(x => new
                {
                    x.Name,
                    x.Length,
                    LastWriteTime = x.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToList();

            return Ok(new
            {
                ok = true,
                root = _root,
                exists = Directory.Exists(_root),
                fileCount = files.Count,
                files
            });
        }

        // DELETE /api/recordings/delete?sessionId={guid}
        [HttpDelete("delete")]
        public IActionResult Delete([FromQuery] Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                return BadRequest("sessionId boş olamaz.");
            }

            var camPath = Path.Combine(_root, $"{sessionId:N}-cam.webm");
            var screenPath = Path.Combine(_root, $"{sessionId:N}-screen.webm");

            var camDeleted = SafeDelete(camPath);
            var screenDeleted = SafeDelete(screenPath);

            return Ok(new
            {
                ok = true,
                sessionId,
                camDeleted,
                screenDeleted
            });
        }

        private string[] GetExistingFileNames()
        {
            try
            {
                Directory.CreateDirectory(_root);

                return new DirectoryInfo(_root)
                    .GetFiles("*.webm")
                    .OrderByDescending(x => x.LastWriteTimeUtc)
                    .Take(20)
                    .Select(x => $"{x.Name} ({x.Length} byte)")
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static bool SafeDelete(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static string? NormalizeKind(string? kind)
        {
            var normalized = string.IsNullOrWhiteSpace(kind)
                ? "cam"
                : kind.Trim().ToLowerInvariant();

            return normalized switch
            {
                "cam" => "cam",
                "camera" => "cam",
                "candidate" => "cam",
                "aday" => "cam",

                "screen" => "screen",
                "display" => "screen",
                "device" => "screen",
                "cihaz" => "screen",
                "ekran" => "screen",

                _ => null
            };
        }
    }
}