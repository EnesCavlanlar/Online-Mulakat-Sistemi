using System;
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
        var inv = await _invRepo.FirstOrDefaultAsync(x => x.Token == token);
        if (inv == null)
            throw new UserFriendlyException("Davet bulunamadı.");

        if (inv.IsUsed)
            throw new UserFriendlyException("Bu davet zaten kullanılmış.");

        if (inv.ExpireAt < DateTime.UtcNow)
            throw new UserFriendlyException("Davetin süresi geçmiş.");

        var session = new ExamSession(
            GuidGenerator.Create(),
            inv.TestId,
            inv.CandidateId,
            Clock.Now // UTC
        );

        await _sessionRepo.InsertAsync(session, true);

        inv.MarkUsed();
        await _invRepo.UpdateAsync(inv, true);

        return new StartByTokenResultDto
        {
            Id = session.Id,
            TestId = inv.TestId,
            CandidateId = inv.CandidateId,
            CandidateName = "" // Blazor tarafında gerekirse CandidateApp'ten çekeriz
        };
    }

    // -------------------- PROCTORING EVENT / İHLAL KAYDI --------------------

    public async Task RecordEventAsync(Guid sessionId, ProctoringEventType type, string? detail)
    {
        // Oturumu çek
        var session = await _sessionRepo.GetAsync(sessionId);

        if (session.IsCancelled)
        {
            // İptal edilmiş oturuma yeni ihlal yazmanın anlamı yok ama event'i yine de kaydedebiliriz.
        }

        // Event kaydı
        var ev = new ProctoringEvent(
            GuidGenerator.Create(),
            sessionId,
            type,
            detail
        );
        await _eventRepo.InsertAsync(ev, true);

        // Basit kural: Her proctoring event bir ihlal sayılır.
        // (İleride event tipine göre filtreleyebiliriz.)
        session.RegisterViolation();

        var max = GetMaxViolations();
        if (!session.IsCancelled && session.ViolationCount >= max)
        {
            session.Cancel($"Proctoring limiti aşıldı ({session.ViolationCount}/{max}).");
        }

        await _sessionRepo.UpdateAsync(session, true);
    }

    // -------------------- OTURUM İPTAL / BİTİR --------------------

    public async Task CancelAsync(Guid sessionId, string reason)
    {
        var s = await _sessionRepo.GetAsync(sessionId);
        if (!s.IsCancelled)
        {
            s.Cancel(reason);
            await _sessionRepo.UpdateAsync(s, true);
        }
    }

    /// <summary>
    /// Oturumu normal şekilde bitirir. İstenirse skor da manuel olarak set edilebilir.
    /// (ComputeAndSaveScoreAsync ile otomatik skor hesapladıysan, bu methodu sadece
    /// oturumu bitirmek için scoreValue=null göndererek kullan.)
    /// </summary>
    public async Task FinishAsync(Guid sessionId, int? scoreValue = null, string? scoreNote = null)
    {
        var s = await _sessionRepo.GetAsync(sessionId);

        if (!s.IsCancelled && s.FinishedAt == null)
        {
            s.Finish(Clock.Now);
            await _sessionRepo.UpdateAsync(s, true);
        }

        if (scoreValue.HasValue)
        {
            // Eski skor varsa sil, manuel yeni skor yaz
            var existing = await _scoreRepo.FirstOrDefaultAsync(x => x.ExamSessionId == sessionId);
            if (existing != null)
            {
                await _scoreRepo.DeleteAsync(existing, true);
            }

            var sc = new Score(
                GuidGenerator.Create(),
                sessionId,
                scoreValue.Value,
                scoreNote
            );

            await _scoreRepo.InsertAsync(sc, true);
        }
    }

    // -------------------- HELPERS --------------------

    private int GetMaxViolations()
    {
        // appsettings.json:
        // "Exam": { "MaxViolations": 2, ... }
        var str = _config["Exam:MaxViolations"];

        if (int.TryParse(str, out var value) && value > 0)
        {
            return value;
        }

        // Default
        return 2;
    }
}

public interface IExamSessionAppService
{
    Task<StartByTokenResultDto> StartByTokenAsync(string token);
    Task RecordEventAsync(Guid sessionId, ProctoringEventType type, string? detail);
    Task CancelAsync(Guid sessionId, string reason);
    Task FinishAsync(Guid sessionId, int? scoreValue = null, string? scoreNote = null);
}
