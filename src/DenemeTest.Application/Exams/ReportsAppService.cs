using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using DenemeTest.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace DenemeTest.Application.Exams
{
    public interface IReportsAppService
    {
        Task<LeaderboardItemDto[]> GetLeaderboardAsync(int take);

        Task<SessionDetailDto> GetSessionDetailAsync(Guid sessionId);

        Task DeleteSessionAsync(Guid sessionId);
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
        private readonly IRepository<ProctoringEvent, Guid> _proctoringEventRepo;
        private readonly IRepository<CodeReview, Guid> _codeReviewRepo;

        public ReportsAppService(
            IRepository<Score, Guid> scoreRepo,
            IRepository<ExamSession, Guid> sessionRepo,
            IRepository<Candidate, Guid> candidateRepo,
            IRepository<Answer, Guid> answerRepo,
            IRepository<Question, Guid> questionRepo,
            IRepository<QuestionOption, Guid> optionRepo,
            IRepository<ProctoringEvent, Guid> proctoringEventRepo,
            IRepository<CodeReview, Guid> codeReviewRepo)
        {
            _scoreRepo = scoreRepo;
            _sessionRepo = sessionRepo;
            _candidateRepo = candidateRepo;
            _answerRepo = answerRepo;
            _questionRepo = questionRepo;
            _optionRepo = optionRepo;
            _proctoringEventRepo = proctoringEventRepo;
            _codeReviewRepo = codeReviewRepo;
        }

        // -------------------- LEADERBOARD --------------------

        public async Task<LeaderboardItemDto[]> GetLeaderboardAsync(int take)
        {
            var safeTake = Math.Max(1, take);

            var scores = (await _scoreRepo.GetListAsync())
                .OrderByDescending(score => score.Value)
                .ThenBy(score => score.CreationTime)
                .Take(safeTake)
                .ToList();

            if (!scores.Any())
            {
                return Array.Empty<LeaderboardItemDto>();
            }

            var sessionIds = scores
                .Select(score => score.ExamSessionId)
                .Distinct()
                .ToList();

            var sessions = await _sessionRepo.GetListAsync(session =>
                sessionIds.Contains(session.Id));

            if (!sessions.Any())
            {
                return Array.Empty<LeaderboardItemDto>();
            }

            var candidateIds = sessions
                .Select(session => session.CandidateId)
                .Distinct()
                .ToList();

            var candidates = await _candidateRepo.GetListAsync(candidate =>
                candidateIds.Contains(candidate.Id));

            var result =
                from score in scores
                join session in sessions on score.ExamSessionId equals session.Id
                join candidate in candidates on session.CandidateId equals candidate.Id
                select new LeaderboardItemDto
                {
                    CandidateId = candidate.Id,
                    FirstName = candidate.FirstName,
                    LastName = candidate.LastName,
                    Email = candidate.Email,

                    Score = score.Value,
                    ExamSessionId = session.Id,

                    IsCancelled = session.IsCancelled,
                    FinishedAt = session.FinishedAt,
                    ViolationCount = session.ViolationCount
                };

            return result.ToArray();
        }

        // -------------------- SESSION DETAY --------------------

        public async Task<SessionDetailDto> GetSessionDetailAsync(Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                throw new UserFriendlyException("Oturum bilgisi geçersiz.");
            }

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
                EndTime = session.FinishedAt,

                Violations = session.ViolationCount,
                IsCancelled = session.IsCancelled,
                CancelReason = session.CancelReason
            };

            await FillAnswersAsync(dto, sessionId);
            await FillCodeReviewsAsync(dto, sessionId);

            return dto;
        }

        private async Task FillAnswersAsync(SessionDetailDto dto, Guid sessionId)
        {
            var answers = await _answerRepo.GetListAsync(answer =>
                answer.ExamSessionId == sessionId);

            if (!answers.Any())
            {
                return;
            }

            var questionIds = answers
                .Select(answer => answer.QuestionId)
                .Distinct()
                .ToList();

            var questions = await _questionRepo.GetListAsync(question =>
                questionIds.Contains(question.Id));

            var options = await _optionRepo.GetListAsync(option =>
                questionIds.Contains(option.QuestionId));

            foreach (var answer in answers)
            {
                var question = questions.FirstOrDefault(questionItem =>
                    questionItem.Id == answer.QuestionId);

                if (question == null)
                {
                    continue;
                }

                List<string>? selectedOptionTexts = null;

                if (answer.SelectedOptionIds != null && answer.SelectedOptionIds.Any())
                {
                    var selectedOptionIdSet = answer.SelectedOptionIds.ToHashSet();

                    selectedOptionTexts = options
                        .Where(option =>
                            option.QuestionId == question.Id &&
                            selectedOptionIdSet.Contains(option.Id))
                        .Select(option => option.Text)
                        .ToList();
                }

                dto.Answers.Add(new QuestionAnswerDetailDto
                {
                    QuestionId = question.Id,
                    QuestionText = question.Text,
                    QuestionType = question.Type.ToString(),

                    SelectedOptions = selectedOptionTexts,
                    TextAnswer = answer.TextAnswer,

                    CodeOutput = null
                });
            }
        }

        private async Task FillCodeReviewsAsync(SessionDetailDto dto, Guid sessionId)
        {
            var codeReviews = await _codeReviewRepo.GetListAsync(review =>
                review.ExamSessionId == sessionId);

            if (!codeReviews.Any())
            {
                return;
            }

            var questionIds = codeReviews
                .Select(review => review.QuestionId)
                .Distinct()
                .ToList();

            var questions = await _questionRepo.GetListAsync(question =>
                questionIds.Contains(question.Id));

            foreach (var review in codeReviews.OrderBy(review => review.CreationTime))
            {
                var question = questions.FirstOrDefault(questionItem =>
                    questionItem.Id == review.QuestionId);

                dto.CodeReviews.Add(new CodeReviewDetailDto
                {
                    QuestionId = review.QuestionId,
                    QuestionText = question?.Text ?? string.Empty,

                    TestsPassed = review.TestsPassed,
                    PassedCount = review.PassedCount,
                    TotalCount = review.TotalCount,

                    IsSuspicious = review.IsSuspicious,
                    QualityScore = review.QualityScore,

                    Summary = review.Summary,
                    Flags = review.Flags,
                    Provider = review.Provider,

                    CreationTime = review.CreationTime
                });
            }
        }

        // -------------------- SESSION SİL --------------------

        [UnitOfWork]
        public async Task DeleteSessionAsync(Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                throw new UserFriendlyException("Silinecek sınav oturumu bulunamadı.");
            }

            var session = await _sessionRepo.GetAsync(sessionId);

            var answers = await _answerRepo.GetListAsync(answer =>
                answer.ExamSessionId == sessionId);

            foreach (var answer in answers)
            {
                await _answerRepo.DeleteAsync(answer, autoSave: false);
            }

            var proctoringEvents = await _proctoringEventRepo.GetListAsync(proctoringEvent =>
                proctoringEvent.ExamSessionId == sessionId);

            foreach (var proctoringEvent in proctoringEvents)
            {
                await _proctoringEventRepo.DeleteAsync(proctoringEvent, autoSave: false);
            }

            var scores = await _scoreRepo.GetListAsync(score =>
                score.ExamSessionId == sessionId);

            foreach (var score in scores)
            {
                await _scoreRepo.DeleteAsync(score, autoSave: false);
            }

            var codeReviews = await _codeReviewRepo.GetListAsync(review =>
                review.ExamSessionId == sessionId);

            foreach (var codeReview in codeReviews)
            {
                await _codeReviewRepo.DeleteAsync(codeReview, autoSave: false);
            }

            await _sessionRepo.DeleteAsync(session, autoSave: true);
        }
    }

    // -------------------- DTO'LAR --------------------

    public class SessionDetailDto
    {
        public Guid SessionId { get; set; }

        public Guid CandidateId { get; set; }

        public string CandidateFirstName { get; set; } = string.Empty;

        public string CandidateLastName { get; set; } = string.Empty;

        public string CandidateEmail { get; set; } = string.Empty;

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public int Violations { get; set; }

        public bool IsCancelled { get; set; }

        public string? CancelReason { get; set; }

        public List<QuestionAnswerDetailDto> Answers { get; set; } = new();

        public List<CodeReviewDetailDto> CodeReviews { get; set; } = new();
    }

    public class QuestionAnswerDetailDto
    {
        public Guid QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public string QuestionType { get; set; } = string.Empty;

        public List<string>? SelectedOptions { get; set; }

        public string? TextAnswer { get; set; }

        public string? CodeOutput { get; set; }
    }

    public class CodeReviewDetailDto
    {
        public Guid QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public bool TestsPassed { get; set; }

        public int PassedCount { get; set; }

        public int TotalCount { get; set; }

        public bool IsSuspicious { get; set; }

        public int? QualityScore { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string Flags { get; set; } = string.Empty;

        public string Provider { get; set; } = string.Empty;

        public DateTime CreationTime { get; set; }
    }
}