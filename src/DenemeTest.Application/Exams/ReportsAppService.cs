using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DenemeTest.Application.Exams;

public class ReportsAppService : ApplicationService, IReportsAppService
{
    private readonly IRepository<Score, Guid> _scoreRepo;
    private readonly IRepository<ExamSession, Guid> _sessionRepo;
    private readonly IRepository<Candidate, Guid> _candidateRepo;

    public ReportsAppService(
        IRepository<Score, Guid> scoreRepo,
        IRepository<ExamSession, Guid> sessionRepo,
        IRepository<Candidate, Guid> candidateRepo)
    {
        _scoreRepo = scoreRepo;
        _sessionRepo = sessionRepo;
        _candidateRepo = candidateRepo;
    }

    public async Task<LeaderboardItemDto[]> GetLeaderboardAsync(int take)
    {
        var scores = (await _scoreRepo.GetListAsync())
            .OrderByDescending(s => s.Value)
            .ThenBy(s => s.CreationTime)
            .Take(Math.Max(1, take))
            .ToList();

        var sessionIds = scores.Select(s => s.ExamSessionId).Distinct().ToList();
        var sessions = await _sessionRepo.GetListAsync(x => sessionIds.Contains(x.Id));
        var candidateIds = sessions.Select(s => s.CandidateId).Distinct().ToList();
        var candidates = await _candidateRepo.GetListAsync(x => candidateIds.Contains(x.Id));

        var result = (from sc in scores
                      join se in sessions on sc.ExamSessionId equals se.Id
                      join ca in candidates on se.CandidateId equals ca.Id
                      select new LeaderboardItemDto
                      {
                          CandidateId = ca.Id,
                          FirstName = ca.FirstName,
                          LastName = ca.LastName,
                          Email = ca.Email,
                          Score = sc.Value,
                          ExamSessionId = se.Id
                      }).ToArray();

        return result;
    }
}

public interface IReportsAppService
{
    Task<LeaderboardItemDto[]> GetLeaderboardAsync(int take);
}
