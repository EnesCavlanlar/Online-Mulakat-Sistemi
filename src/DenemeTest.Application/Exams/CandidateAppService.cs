using System;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
//using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DenemeTest.Application.Exams;

public class CandidateAppService : CrudAppService<
    Candidate, CandidateDto, Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateCandidateDto, CreateUpdateCandidateDto>, ICandidateAppService
{
    private readonly IRepository<Score, Guid> _scoreRepo;

    public CandidateAppService(IRepository<Candidate, Guid> repository,
                               IRepository<Score, Guid> scoreRepo) : base(repository)
    {
        _scoreRepo = scoreRepo;
    }

    public override async Task<PagedResultDto<CandidateDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var result = await base.GetListAsync(input);
        var ids = result.Items.Select(i => i.Id).ToList();

        // IRepository ile doğru kullanım
        var lastScores = await _scoreRepo.GetListAsync(s => ids.Contains(s.ExamSessionId));

        // Şimdilik LastScore = null; raporu sonra işleyeceğiz.
        return result;
    }

}

public interface ICandidateAppService : ICrudAppService<
    CandidateDto, Guid, PagedAndSortedResultRequestDto,
    CreateUpdateCandidateDto, CreateUpdateCandidateDto>
{ }
