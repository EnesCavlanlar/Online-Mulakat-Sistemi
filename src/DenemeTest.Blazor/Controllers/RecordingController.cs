using Microsoft.Extensions.Configuration;

using DenemeTest.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;

namespace DenemeTest.Blazor.Controllers;

[Route("api/recordings")]
[ApiController]
[AllowAnonymous] // Davetli kullanıcılar için açıksa kalsın
public class RecordingController : ControllerBase
{
    private readonly IBlobContainer<ExamRecordingContainer> _container;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _cfg;

    public RecordingController(IBlobContainer<ExamRecordingContainer> container,
                               IWebHostEnvironment env,
                               IConfiguration cfg)
    {
        _container = container;
        _env = env;
        _cfg = cfg;
    }

    private static string Sanitize(string s)
        => string.Concat(s.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));

    private string GetTempRoot()
    {
        // disk üzerinde geçici parçaları tutup finalize'da blob'a yazacağız
        var root = _cfg["Exam:RecordingTemp"] ?? Path.Combine(_env.ContentRootPath, "App_Data", "recordings_tmp");
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);
        return root;
    }

    [HttpPost("chunk")]
    [IgnoreAntiforgeryToken]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadChunk([FromQuery] string sessionId, [FromQuery] long seq)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId required");
        var sid = Sanitize(sessionId);
        var root = GetTempRoot();
        var folder = Path.Combine(root, sid);
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        var partialPath = Path.Combine(folder, $"{seq:D10}.part");
        using (var fs = System.IO.File.Create(partialPath))
        {
            await Request.Body.CopyToAsync(fs);
        }
        return Ok();
    }

    [HttpPost("finalize")]
    [IgnoreAntiforgeryToken]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Finalize([FromQuery] string sessionId, [FromQuery] string mime = "video/webm")
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId required");
        var sid = Sanitize(sessionId);
        var root = GetTempRoot();
        var folder = Path.Combine(root, sid);
        if (!Directory.Exists(folder))
            return NotFound("no chunks");

        var parts = Directory.GetFiles(folder, "*.part").OrderBy(p => p).ToArray();
        if (parts.Length == 0) return BadRequest("no parts");

        // parçaları tek dosyada birleştir → blob'a yaz
        await using var ms = new MemoryStream();
        foreach (var p in parts)
        {
            await using var inFs = System.IO.File.OpenRead(p);
            await inFs.CopyToAsync(ms);
        }
        ms.Position = 0;

        var ext = mime.Contains("webm", StringComparison.OrdinalIgnoreCase) ? ".webm" : ".dat";
        var blobName = $"{sid}{ext}";

        await _container.SaveAsync(blobName, ms);

        // geçici dosyaları temizle
        foreach (var p in parts)
            System.IO.File.Delete(p);
        Directory.Delete(folder, true);

        return Ok(new { blob = blobName });
    }

    [HttpPost("cancel")]
    [IgnoreAntiforgeryToken]
    public IActionResult Cancel([FromQuery] string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return Ok();
        var sid = Sanitize(sessionId);
        var folder = Path.Combine(GetTempRoot(), sid);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        return Ok();
    }

    // --- Admin indirme ucu (istersen sadece Admin authorize et) ---
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string sessionId)
    {
        var sid = Sanitize(sessionId);
        var tryNames = new[] { $"{sid}.webm", $"{sid}.dat" };
        foreach (var name in tryNames)
        {
            if (await _container.ExistsAsync(name))
            {
                var bytes = await _container.GetAllBytesAsync(name);
                var ct = name.EndsWith(".webm") ? "video/webm" : "application/octet-stream";
                return File(bytes, ct, name);
            }
        }
        return NotFound();
    }
}
