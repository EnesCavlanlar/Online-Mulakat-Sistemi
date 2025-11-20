using DenemeTest.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;

namespace DenemeTest.Blazor.Controllers;

[Route("api/recordings")]
[ApiController]
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

    // ... sende zaten olan chunk upload aksiyonlarını aynen bırak ...

    [HttpPost("finalize")]
    public async Task<IActionResult> FinalizeAsync([FromForm] Guid sessionId)
    {
        // Geçici klasör: appsettings.Exam.RecordingTemp
        var tempRoot = _config["Exam:RecordingTemp"] ?? "App_Data/recordings_tmp";
        var mergedPath = Path.Combine(tempRoot, $"{sessionId}.webm");

        if (!System.IO.File.Exists(mergedPath))
        {
            return BadRequest("Geçici kayıt dosyası bulunamadı.");
        }

        await using (var fs = System.IO.File.OpenRead(mergedPath))
        {
            var blobName = GetBlobName(sessionId);
            await _recordings.SaveAsync(blobName, fs, overrideExisting: true);
        }

        System.IO.File.Delete(mergedPath);

        return Ok(new { ok = true });
    }

    [HttpPost("cancel")]
    public IActionResult Cancel([FromForm] Guid sessionId)
    {
        var tempRoot = _config["Exam:RecordingTemp"] ?? "App_Data/recordings_tmp";
        var mergedPath = Path.Combine(tempRoot, $"{sessionId}.webm");

        if (System.IO.File.Exists(mergedPath))
        {
            System.IO.File.Delete(mergedPath);
        }

        return Ok();
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Download(Guid sessionId)
    {
        var blobName = GetBlobName(sessionId);
        if (!await _recordings.ExistsAsync(blobName))
        {
            return NotFound();
        }

        var stream = await _recordings.GetAsync(blobName);
        return File(stream, "video/webm", $"{sessionId}.webm");
    }

    private static string GetBlobName(Guid sessionId)
        => $"recordings/{sessionId}.webm";
}
