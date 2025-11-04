using System;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
//using Microsoft.EntityFrameworkCore;
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

    public ExamSessionAppService(
        IRepository<ExamInvitation, Guid> invRepo,
        IRepository<ExamSession, Guid> sessionRepo,
        IRepository<ProctoringEvent, Guid> eventRepo,
        IRepository<Score, Guid> scoreRepo)
    {
        _invRepo = invRepo;
        _sessionRepo = sessionRepo;
        _eventRepo = eventRepo;
        _scoreRepo = scoreRepo;
    }

    public async Task<StartByTokenResultDto> StartByTokenAsync(string token)
    {
        var inv = await _invRepo.FirstOrDefaultAsync(x => x.Token == token);
        if (inv == null) throw new UserFriendlyException("Davet bulunamadı.");
        if (inv.IsUsed) throw new UserFriendlyException("Bu davet zaten kullanılmış.");
        if (inv.ExpireAt < DateTime.UtcNow) throw new UserFriendlyException("Davetin süresi geçmiş.");

        var session = new ExamSession(GuidGenerator.Create(), inv.TestId, inv.CandidateId, DateTime.UtcNow);
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

    public async Task RecordEventAsync(Guid sessionId, ProctoringEventType type, string? detail)
    {
        var ev = new ProctoringEvent(GuidGenerator.Create(), sessionId, type, detail);
        await _eventRepo.InsertAsync(ev, true);
    }

    public async Task CancelAsync(Guid sessionId, string reason)
    {
        var s = await _sessionRepo.GetAsync(sessionId);
        if (!s.IsCancelled) { s.Cancel(reason); await _sessionRepo.UpdateAsync(s, true); }
    }

    public async Task FinishAsync(Guid sessionId, int? scoreValue = null, string? scoreNote = null)
    {
        var s = await _sessionRepo.GetAsync(sessionId);
        if (s.FinishedAt == null) { s.Finish(DateTime.UtcNow); await _sessionRepo.UpdateAsync(s, true); }

        if (scoreValue.HasValue)
        {
            var sc = new Score(GuidGenerator.Create(), sessionId, scoreValue.Value, scoreNote);
            await _scoreRepo.InsertAsync(sc, true);
        }
    }
}

public interface IExamSessionAppService
{
    Task<StartByTokenResultDto> StartByTokenAsync(string token);
    Task RecordEventAsync(Guid sessionId, ProctoringEventType type, string? detail);
    Task CancelAsync(Guid sessionId, string reason);
    Task FinishAsync(Guid sessionId, int? scoreValue = null, string? scoreNote = null);
}
