using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DenemeTest.Application.Exams;

public class ExamSessionAppService : ApplicationService, IExamSessionAppService
{
    private readonly IRepository<ExamInvitation, Guid> _invRepo;
    private readonly IRepository<ExamSession, Guid> _sessionRepo;
    private readonly IRepository<ProctoringEvent, Guid> _eventRepo;
    private readonly IRepository<Score, Guid> _scoreRepo;
    private readonly IConfiguration _config;

    public ExamSessionAppService(
        IRepository<ExamInvitation, Guid> invRepo,
        IRepository<ExamSession, Guid> sessionRepo,
        IRepository<ProctoringEvent, Guid> eventRepo,
        IRepository<Score, Guid> scoreRepo,
        IConfiguration config)
    {
        _invRepo = invRepo;
        _sessionRepo = sessionRepo;
        _eventRepo = eventRepo;
        _scoreRepo = scoreRepo;
        _config = config;
    }

    // -------------------- TOKEN İLE OTURUM BAŞLAT --------------------

    public async Task<StartByTokenResultDto> StartByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UserFriendlyException("Davet tokenı boş olamaz.");
        }

        var tokenHash = HashToken(token);

        var inv = await _invRepo.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        if (inv == null)
        {
            throw new UserFriendlyException("Davet bulunamadı.");
        }

        if (inv.IsUsed)
        {
            throw new UserFriendlyException("Bu davet zaten kullanılmış.");
        }

        if (inv.ExpireAt <= DateTime.UtcNow)
        {
            throw new UserFriendlyException("Davetin süresi geçmiş.");
        }

        // Aynı aday + aynı test için bitmemiş/iptal edilmemiş tek aktif oturum kuralı
        var alreadyActive = await _sessionRepo.FirstOrDefaultAsync(s =>
            s.TestId == inv.TestId &&
            s.CandidateId == inv.CandidateId &&
            !s.IsCancelled &&
            s.FinishedAt == null
        );

        if (alreadyActive != null)
        {
            throw new UserFriendlyException("Bu test için zaten aktif bir oturum var.");
        }

        var session = new ExamSession(
            GuidGenerator.Create(),
            inv.TestId,
            inv.CandidateId,
            Clock.Now
        );

        await _sessionRepo.InsertAsync(session, autoSave: true);

        // Token tek kullanımlık
        inv.MarkUsed();
        await _invRepo.UpdateAsync(inv, autoSave: true);

        return new StartByTokenResultDto
        {
            Id = session.Id,
            TestId = inv.TestId,
            CandidateId = inv.CandidateId,
            CandidateName = ""
        };
    }

    // -------------------- PROCTORING EVENT / İHLAL KAYDI --------------------

    public async Task RecordEventAsync(Guid sessionId, ProctoringEventType type, string? detail)
    {
        var session = await _sessionRepo.GetAsync(sessionId);

        var ev = new ProctoringEvent(GuidGenerator.Create(), sessionId, type, detail);
        await _eventRepo.InsertAsync(ev, autoSave: true);

        // Basit kural: her event ihlal sayılır
        session.RegisterViolation();

        var max = GetMaxViolations();
        if (!session.IsCancelled && session.ViolationCount >= max)
        {
            session.Cancel($"Proctoring limiti aşıldı ({session.ViolationCount}/{max}).");
        }

        await _sessionRepo.UpdateAsync(session, autoSave: true);
    }

    // -------------------- OTURUM İPTAL / BİTİR --------------------

    public async Task CancelAsync(Guid sessionId, string reason)
    {
        var session = await _sessionRepo.GetAsync(sessionId);

        if (!session.IsCancelled && session.FinishedAt == null)
        {
            session.Cancel(reason);
            await _sessionRepo.UpdateAsync(session, autoSave: true);
        }
    }

    public async Task FinishAsync(Guid sessionId, int? scoreValue = null, string? scoreNote = null)
    {
        var session = await _sessionRepo.GetAsync(sessionId);

        if (!session.IsCancelled && session.FinishedAt == null)
        {
            session.Finish(Clock.Now);
            await _sessionRepo.UpdateAsync(session, autoSave: true);
        }

        if (scoreValue.HasValue)
        {
            var existing = await _scoreRepo.FirstOrDefaultAsync(x => x.ExamSessionId == sessionId);
            if (existing != null)
            {
                await _scoreRepo.DeleteAsync(existing, autoSave: true);
            }

            var score = new Score(
                GuidGenerator.Create(),
                sessionId,
                scoreValue.Value,
                scoreNote
            );

            await _scoreRepo.InsertAsync(score, autoSave: true);
        }
    }

    // -------------------- HELPERS --------------------

    private int GetMaxViolations()
    {
        var str = _config["Exam:MaxViolations"];

        if (int.TryParse(str, out var value) && value > 0)
        {
            return value;
        }

        return 2;
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public interface IExamSessionAppService
{
    Task<StartByTokenResultDto> StartByTokenAsync(string token);
    Task RecordEventAsync(Guid sessionId, ProctoringEventType type, string? detail);
    Task CancelAsync(Guid sessionId, string reason);
    Task FinishAsync(Guid sessionId, int? scoreValue = null, string? scoreNote = null);
}