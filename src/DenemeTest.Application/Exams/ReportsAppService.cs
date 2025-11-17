using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using DenemeTest.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DenemeTest.Application.Exams
{
    public interface IReportsAppService
    {
        Task<LeaderboardItemDto[]> GetLeaderboardAsync(int take);
        Task<SessionDetailDto> GetSessionDetailAsync(Guid sessionId);
    }

    [Authorize(DenemeTestPermissions.Exams.Reports)]
    public class ReportsAppService : ApplicationService, IReportsAppService
    {
        private readonly IRepository<Score, Guid> _scoreRepo;
        private readonly IRepository<ExamSession, Guid> _sessionRepo;
        private readonly IRepository<Candidate, Guid> _candidateRepo;
        private readonly IRepository<Answer, Guid> _answerRepo;
        private readonly IRepository<Question, Guid> _questionRepo;
        private readonly IRepository<QuestionOption, Guid> _optionRepo;

        public ReportsAppService(
            IRepository<Score, Guid> scoreRepo,
            IRepository<ExamSession, Guid> sessionRepo,
            IRepository<Candidate, Guid> candidateRepo,
            IRepository<Answer, Guid> answerRepo,
            IRepository<Question, Guid> questionRepo,
            IRepository<QuestionOption, Guid> optionRepo)
        {
            _scoreRepo = scoreRepo;
            _sessionRepo = sessionRepo;
            _candidateRepo = candidateRepo;
            _answerRepo = answerRepo;
            _questionRepo = questionRepo;
            _optionRepo = optionRepo;
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

            return (from sc in scores
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
        }

        public async Task<SessionDetailDto> GetSessionDetailAsync(Guid sessionId)
        {
            var session = await _sessionRepo.GetAsync(sessionId);
            var candidate = await _candidateRepo.GetAsync(session.CandidateId);

            var dto = new SessionDetailDto
            {
                SessionId = session.Id,
                CandidateId = candidate.Id,
                CandidateFirstName = candidate.FirstName,
                CandidateLastName = candidate.LastName,
                CandidateEmail = candidate.Email,
                StartTime = session.StartedAt,
                EndTime = null,
                Violations = 0
            };

            var answers = await _answerRepo.GetListAsync(a => a.ExamSessionId == sessionId);
            var qIds = answers.Select(a => a.QuestionId).Distinct().ToList();
            var questions = await _questionRepo.GetListAsync(q => qIds.Contains(q.Id));
            var options = await _optionRepo.GetListAsync(o => qIds.Contains(o.QuestionId));

            foreach (var ans in answers)
            {
                var q = questions.First(x => x.Id == ans.QuestionId);

                List<string>? selectedOptionTexts = null;
                if (ans.SelectedOptionIds != null && ans.SelectedOptionIds.Any())
                {
                    var set = ans.SelectedOptionIds.ToHashSet();
                    selectedOptionTexts = options
                        .Where(o => o.QuestionId == q.Id && set.Contains(o.Id))
                        .Select(o => o.Text)
                        .ToList();
                }

                dto.Answers.Add(new QuestionAnswerDetailDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.Text,
                    QuestionType = q.Type.ToString(),
                    SelectedOptions = selectedOptionTexts,
                    TextAnswer = ans.TextAnswer,
                    // Answer entity'de alan olmadığı için null geçiyoruz
                    CodeOutput = null
                });
            }

            return dto;
        }
    }

    // ====== PUBLIC DTO’lar (Application katmanında) ======

    public class SessionDetailDto
    {
        public Guid SessionId { get; set; }

        public Guid CandidateId { get; set; }
        public string CandidateFirstName { get; set; } = default!;
        public string CandidateLastName { get; set; } = default!;
        public string CandidateEmail { get; set; } = default!;

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public int Violations { get; set; }

        public List<QuestionAnswerDetailDto> Answers { get; set; } = new();
    }

    public class QuestionAnswerDetailDto
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = default!;
        public string QuestionType { get; set; } = default!; // "MultipleChoice/Classic/Coding"

        public List<string>? SelectedOptions { get; set; }  // MCQ için seçilen şık metinleri
        public string? TextAnswer { get; set; }             // Classic için text
        public string? CodeOutput { get; set; }             // Coding için son çalışma çıktısı (yoksa null)
    }
}
