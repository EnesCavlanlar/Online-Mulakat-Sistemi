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
using Volo.Abp.Uow;

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

    [UnitOfWork]
    public async Task<StartByTokenResultDto> StartByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UserFriendlyException("Davet tokenı boş olamaz.");
        }

        var tokenHash = HashToken(token);

        var invitation = await _invRepo.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        if (invitation == null)
        {
            throw new UserFriendlyException("Davet bulunamadı.");
        }

        if (invitation.IsUsed)
        {
            throw new UserFriendlyException("Bu davet zaten kullanılmış. Aynı link ile tekrar sınava girilemez.");
        }

        if (invitation.ExpireAt <= DateTime.UtcNow)
        {
            throw new UserFriendlyException("Davetin süresi geçmiş.");
        }

        var alreadyActiveSession = await _sessionRepo.FirstOrDefaultAsync(session =>
            session.TestId == invitation.TestId &&
            session.CandidateId == invitation.CandidateId &&
            !session.IsCancelled &&
            session.FinishedAt == null
        );

        if (alreadyActiveSession != null)
        {
            invitation.MarkUsed();
            await _invRepo.UpdateAsync(invitation, autoSave: true);

            throw new UserFriendlyException("Bu test için zaten aktif bir oturum var. Aynı davetle ikinci kez giriş yapılamaz.");
        }

        invitation.MarkUsed();
        await _invRepo.UpdateAsync(invitation, autoSave: true);

        var examSession = new ExamSession(
            GuidGenerator.Create(),
            invitation.TestId,
            invitation.CandidateId,
            Clock.Now
        );

        await _sessionRepo.InsertAsync(examSession, autoSave: true);

        return new StartByTokenResultDto
        {
            Id = examSession.Id,
            TestId = invitation.TestId,
            CandidateId = invitation.CandidateId,
            CandidateName = string.Empty
        };
    }

    // -------------------- PROCTORING EVENT / İHLAL KAYDI --------------------

    [UnitOfWork]
    public async Task RecordEventAsync(Guid sessionId, ProctoringEventType type, string? detail)
    {
        var session = await _sessionRepo.GetAsync(sessionId);

        if (session.IsCancelled || session.FinishedAt != null)
        {
            return;
        }

        var proctoringEvent = new ProctoringEvent(
            GuidGenerator.Create(),
            sessionId,
            type,
            detail
        );

        await _eventRepo.InsertAsync(proctoringEvent, autoSave: true);

        session.RegisterViolation();

        var maxViolationCount = GetMaxViolations();

        if (!session.IsCancelled && session.ViolationCount >= maxViolationCount)
        {
            session.Cancel($"Proctoring limiti aşıldı ({session.ViolationCount}/{maxViolationCount}).");
        }

        await _sessionRepo.UpdateAsync(session, autoSave: true);
    }

    // -------------------- OTURUM İPTAL --------------------

    [UnitOfWork]
    public async Task CancelAsync(Guid sessionId, string reason)
    {
        var session = await _sessionRepo.GetAsync(sessionId);

        if (session.IsCancelled || session.FinishedAt != null)
        {
            return;
        }

        session.Cancel(string.IsNullOrWhiteSpace(reason)
            ? "Sınav iptal edildi."
            : reason);

        await _sessionRepo.UpdateAsync(session, autoSave: true);
    }

    // -------------------- OTURUM BİTİR --------------------

    [UnitOfWork]
    public async Task FinishAsync(Guid sessionId, int? scoreValue = null, string? scoreNote = null)
    {
        var session = await _sessionRepo.GetAsync(sessionId);

        if (session.IsCancelled)
        {
            return;
        }

        if (session.FinishedAt == null)
        {
            session.Finish(Clock.Now);
            await _sessionRepo.UpdateAsync(session, autoSave: true);
        }

        if (!scoreValue.HasValue)
        {
            return;
        }

        var normalizedScore = NormalizeScore(scoreValue.Value);

        var existingScore = await _scoreRepo.FirstOrDefaultAsync(x => x.ExamSessionId == sessionId);
        if (existingScore != null)
        {
            await _scoreRepo.DeleteAsync(existingScore, autoSave: true);
        }

        var score = new Score(
            GuidGenerator.Create(),
            sessionId,
            normalizedScore,
            scoreNote
        );

        await _scoreRepo.InsertAsync(score, autoSave: true);
    }

    // -------------------- HELPERS --------------------

    private int GetMaxViolations()
    {
        var valueFromConfig = _config["Exam:MaxViolations"];

        if (int.TryParse(valueFromConfig, out var value) && value > 0)
        {
            return value;
        }

        return 2;
    }

    private static int NormalizeScore(int score)
    {
        if (score < 0)
        {
            return 0;
        }

        if (score > 100)
        {
            return 100;
        }

        return score;
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