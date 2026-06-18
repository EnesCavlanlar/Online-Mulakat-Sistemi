using DenemeTest.Exams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace DenemeTest.Blazor.Controllers
{
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/recordings")]
    public class RecordingController : ControllerBase
    {
        private readonly string _root;
        private readonly IWebHostEnvironment _env;
        private readonly IRepository<ExamRecording, Guid> _recordingRepository;
        private readonly IRepository<ExamSession, Guid> _sessionRepository;

        public RecordingController(
            IConfiguration config,
            IWebHostEnvironment env,
            IRepository<ExamRecording, Guid> recordingRepository,
            IRepository<ExamSession, Guid> sessionRepository)
        {
            _env = env;
            _recordingRepository = recordingRepository;
            _sessionRepository = sessionRepository;

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
        [AllowAnonymous]
        [HttpPost("finalize-upload")]
        [DisableRequestSizeLimit]
        [RequestSizeLimit(2L * 1024L * 1024L * 1024L)]
        [UnitOfWork]
        public async Task<IActionResult> FinalizeUpload(
            [FromQuery] Guid sessionId,
            [FromQuery] string? mime,
            [FromQuery] string? kind)
        {
            if (sessionId == Guid.Empty)
            {
                return BadRequest("sessionId boş olamaz.");
            }

            var kindEnum = NormalizeKindEnum(kind);

            if (kindEnum == null)
            {
                return BadRequest("kind sadece cam veya screen olabilir.");
            }

            if (Request.Body == null)
            {
                return BadRequest("Kayıt verisi bulunamadı.");
            }

            var session = await _sessionRepository.FindAsync(sessionId);

            if (session == null)
            {
                return BadRequest("Bu sessionId için sınav oturumu bulunamadı.");
            }

            Directory.CreateDirectory(_root);

            var normalizedKind = ToStorageKind(kindEnum.Value);
            var safeMime = NormalizeMimeType(mime);

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

                if (!finalInfo.Exists || finalInfo.Length <= 0)
                {
                    return StatusCode(500, new
                    {
                        ok = false,
                        sessionId,
                        kind = normalizedKind,
                        error = "Final kayıt dosyası oluşturulamadı."
                    });
                }

                var uploadedAt = DateTime.UtcNow;
                var expiresAt = uploadedAt.AddDays(30);

                var existingRecording = await _recordingRepository.FirstOrDefaultAsync(x =>
                    x.ExamSessionId == sessionId &&
                    x.Kind == kindEnum.Value
                );

                if (existingRecording == null)
                {
                    var recording = new ExamRecording(
                        Guid.NewGuid(),
                        sessionId,
                        kindEnum.Value,
                        fileName,
                        finalPath,
                        safeMime,
                        finalInfo.Length,
                        uploadedAt,
                        expiresAt
                    );

                    await _recordingRepository.InsertAsync(recording, autoSave: true);
                }
                else
                {
                    existingRecording.UpdateFileInfo(
                        fileName,
                        finalPath,
                        safeMime,
                        finalInfo.Length,
                        uploadedAt,
                        expiresAt
                    );

                    await _recordingRepository.UpdateAsync(existingRecording, autoSave: true);
                }

                return Ok(new
                {
                    ok = true,
                    sessionId,
                    kind = normalizedKind,
                    fileName,
                    size = finalInfo.Length,
                    mime = safeMime,
                    root = _root,
                    savedPath = finalPath,
                    uploadedAt,
                    expiresAt
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
        [Authorize]
        [HttpGet("download")]
        public async Task<IActionResult> Download(
            [FromQuery] Guid sessionId,
            [FromQuery] string? kind = "cam")
        {
            if (sessionId == Guid.Empty)
            {
                return BadRequest("sessionId boş olamaz.");
            }

            var kindEnum = NormalizeKindEnum(kind);

            if (kindEnum == null)
            {
                return BadRequest("kind sadece cam veya screen olabilir.");
            }

            var normalizedKind = ToStorageKind(kindEnum.Value);

            var recording = await _recordingRepository.FirstOrDefaultAsync(x =>
                x.ExamSessionId == sessionId &&
                x.Kind == kindEnum.Value &&
                !x.IsStorageDeleted
            );

            string fileName;
            string path;
            string contentType;

            if (recording != null)
            {
                fileName = recording.FileName;
                path = recording.StoragePath;
                contentType = NormalizeMimeType(recording.MimeType);
            }
            else
            {
                fileName = $"{sessionId:N}-{normalizedKind}.webm";
                path = Path.Combine(_root, fileName);
                contentType = "video/webm";
            }

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
                    metadataFound = recording != null,
                    existingFiles = GetExistingFileNames()
                });
            }

            var fileInfo = new FileInfo(path);

            if (fileInfo.Length <= 0)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Kayıt dosyası var ancak boş görünüyor.",
                    sessionId,
                    kind = normalizedKind,
                    fileName,
                    path
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

            return File(stream, contentType, fileName, enableRangeProcessing: true);
        }

        // GET /api/recordings/exists?sessionId={guid}
        [AllowAnonymous]
        [HttpGet("exists")]
        public async Task<IActionResult> Exists([FromQuery] Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                return BadRequest("sessionId boş olamaz.");
            }

            var cam = await GetRecordingStatusAsync(sessionId, ExamRecordingKind.Cam);
            var screen = await GetRecordingStatusAsync(sessionId, ExamRecordingKind.Screen);

            return Ok(new
            {
                ok = true,
                sessionId,
                root = _root,

                camExists = cam.Exists,
                camFileName = cam.Exists ? cam.FileName : null,
                camSize = cam.Exists ? cam.SizeBytes : 0,
                camMetadataFound = cam.MetadataFound,
                camUploadedAt = cam.UploadedAt,
                camExpiresAt = cam.ExpiresAt,

                screenExists = screen.Exists,
                screenFileName = screen.Exists ? screen.FileName : null,
                screenSize = screen.Exists ? screen.SizeBytes : 0,
                screenMetadataFound = screen.MetadataFound,
                screenUploadedAt = screen.UploadedAt,
                screenExpiresAt = screen.ExpiresAt
            });
        }

        // GET /api/recordings/debug
        [Authorize]
        [HttpGet("debug")]
        public async Task<IActionResult> Debug()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

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

            var latestMetadata = (await _recordingRepository.GetListAsync())
                .OrderByDescending(x => x.UploadedAt)
                .Take(50)
                .Select(x => new
                {
                    x.Id,
                    x.ExamSessionId,
                    Kind = x.Kind.ToString(),
                    x.FileName,
                    x.StoragePath,
                    x.MimeType,
                    x.SizeBytes,
                    x.UploadedAt,
                    x.ExpiresAt,
                    x.IsStorageDeleted,
                    x.StorageDeletedAt,
                    FileExists = System.IO.File.Exists(x.StoragePath)
                })
                .ToList();

            return Ok(new
            {
                ok = true,
                root = _root,
                exists = Directory.Exists(_root),
                fileCount = files.Count,
                files,
                metadataCount = latestMetadata.Count,
                metadata = latestMetadata
            });
        }

        // DELETE /api/recordings/delete?sessionId={guid}
        [Authorize]
        [HttpDelete("delete")]
        [UnitOfWork]
        public async Task<IActionResult> Delete([FromQuery] Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                return BadRequest("sessionId boş olamaz.");
            }

            var camDeleted = await DeleteRecordingKindAsync(sessionId, ExamRecordingKind.Cam);
            var screenDeleted = await DeleteRecordingKindAsync(sessionId, ExamRecordingKind.Screen);

            return Ok(new
            {
                ok = true,
                sessionId,
                camDeleted,
                screenDeleted
            });
        }

        private async Task<RecordingStatus> GetRecordingStatusAsync(Guid sessionId, ExamRecordingKind kind)
        {
            var normalizedKind = ToStorageKind(kind);

            var recording = await _recordingRepository.FirstOrDefaultAsync(x =>
                x.ExamSessionId == sessionId &&
                x.Kind == kind &&
                !x.IsStorageDeleted
            );

            if (recording != null)
            {
                var info = new FileInfo(recording.StoragePath);

                return new RecordingStatus
                {
                    Exists = info.Exists && info.Length > 0,
                    MetadataFound = true,
                    FileName = recording.FileName,
                    SizeBytes = info.Exists ? info.Length : recording.SizeBytes,
                    UploadedAt = recording.UploadedAt,
                    ExpiresAt = recording.ExpiresAt
                };
            }

            var legacyFileName = $"{sessionId:N}-{normalizedKind}.webm";
            var legacyPath = Path.Combine(_root, legacyFileName);
            var legacyInfo = new FileInfo(legacyPath);

            return new RecordingStatus
            {
                Exists = legacyInfo.Exists && legacyInfo.Length > 0,
                MetadataFound = false,
                FileName = legacyFileName,
                SizeBytes = legacyInfo.Exists ? legacyInfo.Length : 0,
                UploadedAt = null,
                ExpiresAt = null
            };
        }

        private async Task<bool> DeleteRecordingKindAsync(Guid sessionId, ExamRecordingKind kind)
        {
            var deleted = false;
            var normalizedKind = ToStorageKind(kind);

            var recording = await _recordingRepository.FirstOrDefaultAsync(x =>
                x.ExamSessionId == sessionId &&
                x.Kind == kind &&
                !x.IsStorageDeleted
            );

            if (recording != null)
            {
                deleted = SafeDelete(recording.StoragePath);

                if (deleted)
                {
                    recording.MarkStorageDeleted(DateTime.UtcNow);
                    await _recordingRepository.UpdateAsync(recording, autoSave: true);
                }

                return deleted;
            }

            var legacyPath = Path.Combine(_root, $"{sessionId:N}-{normalizedKind}.webm");
            return SafeDelete(legacyPath);
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

        private static ExamRecordingKind? NormalizeKindEnum(string? kind)
        {
            var normalized = NormalizeKind(kind);

            return normalized switch
            {
                "cam" => ExamRecordingKind.Cam,
                "screen" => ExamRecordingKind.Screen,
                _ => null
            };
        }

        private static string ToStorageKind(ExamRecordingKind kind)
        {
            return kind switch
            {
                ExamRecordingKind.Cam => "cam",
                ExamRecordingKind.Screen => "screen",
                _ => "unknown"
            };
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

        private static string NormalizeMimeType(string? mime)
        {
            if (string.IsNullOrWhiteSpace(mime))
            {
                return "video/webm";
            }

            var normalized = mime.Trim().ToLowerInvariant();

            if (normalized.StartsWith("video/webm"))
            {
                return "video/webm";
            }

            if (normalized.StartsWith("video/mp4"))
            {
                return "video/mp4";
            }

            if (normalized.StartsWith("audio/webm"))
            {
                return "audio/webm";
            }

            return "application/octet-stream";
        }

        private sealed class RecordingStatus
        {
            public bool Exists { get; set; }

            public bool MetadataFound { get; set; }

            public string? FileName { get; set; }

            public long SizeBytes { get; set; }

            public DateTime? UploadedAt { get; set; }

            public DateTime? ExpiresAt { get; set; }
        }
    }
}