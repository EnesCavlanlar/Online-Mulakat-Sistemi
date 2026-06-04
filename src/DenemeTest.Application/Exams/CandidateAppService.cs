using System;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace DenemeTest.Application.Exams;

public class CandidateAppService : CrudAppService<
    Candidate, CandidateDto, Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateCandidateDto, CreateUpdateCandidateDto>, ICandidateAppService
{
    private readonly IRepository<ExamInvitation, Guid> _invitationRepo;
    private readonly IRepository<ExamSession, Guid> _sessionRepo;
    private readonly IRepository<Answer, Guid> _answerRepo;
    private readonly IRepository<ProctoringEvent, Guid> _proctoringEventRepo;
    private readonly IRepository<Score, Guid> _scoreRepo;

    public CandidateAppService(
        IRepository<Candidate, Guid> repository,
        IRepository<ExamInvitation, Guid> invitationRepo,
        IRepository<ExamSession, Guid> sessionRepo,
        IRepository<Answer, Guid> answerRepo,
        IRepository<ProctoringEvent, Guid> proctoringEventRepo,
        IRepository<Score, Guid> scoreRepo)
        : base(repository)
    {
        _invitationRepo = invitationRepo;
        _sessionRepo = sessionRepo;
        _answerRepo = answerRepo;
        _proctoringEventRepo = proctoringEventRepo;
        _scoreRepo = scoreRepo;
    }

    public override async Task<PagedResultDto<CandidateDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        input.MaxResultCount = input.MaxResultCount <= 0 ? 1000 : input.MaxResultCount;
        return await base.GetListAsync(input);
    }

    [UnitOfWork]
    public override async Task DeleteAsync(Guid id)
    {
        var candidate = await Repository.GetAsync(id);

        var invitations = await _invitationRepo.GetListAsync(x => x.CandidateId == id);
        foreach (var invitation in invitations)
        {
            await _invitationRepo.DeleteAsync(invitation, autoSave: false);
        }

        var sessions = await _sessionRepo.GetListAsync(x => x.CandidateId == id);
        foreach (var session in sessions)
        {
            await DeleteSessionRelatedDataAsync(session.Id);
            await _sessionRepo.DeleteAsync(session, autoSave: false);
        }

        await Repository.DeleteAsync(candidate, autoSave: true);
    }

    private async Task DeleteSessionRelatedDataAsync(Guid sessionId)
    {
        var answers = await _answerRepo.GetListAsync(x => x.ExamSessionId == sessionId);
        foreach (var answer in answers)
        {
            await _answerRepo.DeleteAsync(answer, autoSave: false);
        }

        var proctoringEvents = await _proctoringEventRepo.GetListAsync(x => x.ExamSessionId == sessionId);
        foreach (var proctoringEvent in proctoringEvents)
        {
            await _proctoringEventRepo.DeleteAsync(proctoringEvent, autoSave: false);
        }

        var scores = await _scoreRepo.GetListAsync(x => x.ExamSessionId == sessionId);
        foreach (var score in scores)
        {
            await _scoreRepo.DeleteAsync(score, autoSave: false);
        }
    }
}

public interface ICandidateAppService : ICrudAppService<
    CandidateDto, Guid, PagedAndSortedResultRequestDto,
    CreateUpdateCandidateDto, CreateUpdateCandidateDto>
{
}