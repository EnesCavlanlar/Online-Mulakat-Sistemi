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
        private readonly IRepository<ExamRecording, Guid> _recordingRepo;

        public ReportsAppService(
            IRepository<Score, Guid> scoreRepo,
            IRepository<ExamSession, Guid> sessionRepo,
            IRepository<Candidate, Guid> candidateRepo,
            IRepository<Answer, Guid> answerRepo,
            IRepository<Question, Guid> questionRepo,
            IRepository<QuestionOption, Guid> optionRepo,
            IRepository<ProctoringEvent, Guid> proctoringEventRepo,
            IRepository<CodeReview, Guid> codeReviewRepo,
            IRepository<ExamRecording, Guid> recordingRepo)
        {
            _scoreRepo = scoreRepo;
            _sessionRepo = sessionRepo;
            _candidateRepo = candidateRepo;
            _answerRepo = answerRepo;
            _questionRepo = questionRepo;
            _optionRepo = optionRepo;
            _proctoringEventRepo = proctoringEventRepo;
            _codeReviewRepo = codeReviewRepo;
            _recordingRepo = recordingRepo;
        }

        public async Task<LeaderboardItemDto[]> GetLeaderboardAsync(int take)
        {
            var safeTake = Math.Clamp(take, 1, 500);

            var sessions = await _sessionRepo.GetListAsync();

            if (!sessions.Any())
            {
                return Array.Empty<LeaderboardItemDto>();
            }

            var sessionIds = sessions
                .Select(session => session.Id)
                .Distinct()
                .ToList();

            var scores = await _scoreRepo.GetListAsync(score =>
                sessionIds.Contains(score.ExamSessionId));

            var latestScoreBySession = scores
                .GroupBy(score => score.ExamSessionId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(score => score.CreationTime)
                        .First()
                );

            var candidateIds = sessions
                .Select(session => session.CandidateId)
                .Distinct()
                .ToList();

            var candidates = await _candidateRepo.GetListAsync(candidate =>
                candidateIds.Contains(candidate.Id));

            var candidateById = candidates.ToDictionary(candidate => candidate.Id);

            var result = sessions
                .Where(session => candidateById.ContainsKey(session.CandidateId))
                .Select(session =>
                {
                    var candidate = candidateById[session.CandidateId];

                    latestScoreBySession.TryGetValue(session.Id, out var score);

                    return new LeaderboardItemDto
                    {
                        CandidateId = candidate.Id,
                        FirstName = candidate.FirstName,
                        LastName = candidate.LastName,
                        Email = candidate.Email,

                        Score = score?.Value ?? 0,
                        ExamSessionId = session.Id,

                        IsCancelled = session.IsCancelled,
                        FinishedAt = session.FinishedAt,
                        ViolationCount = session.ViolationCount
                    };
                })
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.FinishedAt.HasValue)
                .ThenByDescending(item => item.ViolationCount)
                .Take(safeTake)
                .ToArray();

            return result;
        }

        public async Task<SessionDetailDto> GetSessionDetailAsync(Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                throw new UserFriendlyException("Oturum bilgisi geçersiz.");
            }

            var session = await _sessionRepo.GetAsync(sessionId);
            var candidate = await _candidateRepo.GetAsync(session.CandidateId);

            var score = await GetLatestScoreAsync(sessionId);

            var dto = new SessionDetailDto
            {
                SessionId = session.Id,

                CandidateId = candidate.Id,
                CandidateFirstName = candidate.FirstName,
                CandidateLastName = candidate.LastName,
                CandidateEmail = candidate.Email,

                StartTime = session.StartedAt,
                EndTime = session.FinishedAt,

                Score = score?.Value,
                ScoreExplanation = score?.Explanation,

                Violations = session.ViolationCount,
                IsCancelled = session.IsCancelled,
                CancelReason = session.CancelReason,

                CandidateRecordingUrl = $"/api/recordings/download?sessionId={session.Id}&kind=cam",
                ScreenRecordingUrl = $"/api/recordings/download?sessionId={session.Id}&kind=screen",
                RecordingExistsUrl = $"/api/recordings/exists?sessionId={session.Id}"
            };

            await FillRecordingsAsync(dto, sessionId);
            await FillAnswersAsync(dto, session);
            await FillCodeReviewsAsync(dto, sessionId);
            await FillProctoringEventsAsync(dto, sessionId);

            return dto;
        }

        private async Task<Score?> GetLatestScoreAsync(Guid sessionId)
        {
            var scores = await _scoreRepo.GetListAsync(score =>
                score.ExamSessionId == sessionId);

            return scores
                .OrderByDescending(score => score.CreationTime)
                .FirstOrDefault();
        }

        private async Task FillRecordingsAsync(SessionDetailDto dto, Guid sessionId)
        {
            var recordings = await _recordingRepo.GetListAsync(recording =>
                recording.ExamSessionId == sessionId);

            if (!recordings.Any())
            {
                dto.HasCandidateRecording = false;
                dto.HasScreenRecording = false;
                return;
            }

            var camRecording = recordings
                .Where(recording => recording.Kind == ExamRecordingKind.Cam)
                .OrderByDescending(recording => recording.UploadedAt)
                .ThenByDescending(recording => recording.CreationTime)
                .FirstOrDefault();

            var screenRecording = recordings
                .Where(recording => recording.Kind == ExamRecordingKind.Screen)
                .OrderByDescending(recording => recording.UploadedAt)
                .ThenByDescending(recording => recording.CreationTime)
                .FirstOrDefault();

            if (camRecording != null)
            {
                dto.HasCandidateRecording =
                    !camRecording.IsStorageDeleted &&
                    camRecording.SizeBytes > 0;

                dto.CandidateRecordingFileName = camRecording.FileName;
                dto.CandidateRecordingSizeBytes = camRecording.SizeBytes;
                dto.CandidateRecordingUploadedAt = camRecording.UploadedAt;
                dto.CandidateRecordingExpiresAt = camRecording.ExpiresAt;
                dto.CandidateRecordingStorageDeleted = camRecording.IsStorageDeleted;
                dto.CandidateRecordingStorageDeletedAt = camRecording.StorageDeletedAt;
            }
            else
            {
                dto.HasCandidateRecording = false;
            }

            if (screenRecording != null)
            {
                dto.HasScreenRecording =
                    !screenRecording.IsStorageDeleted &&
                    screenRecording.SizeBytes > 0;

                dto.ScreenRecordingFileName = screenRecording.FileName;
                dto.ScreenRecordingSizeBytes = screenRecording.SizeBytes;
                dto.ScreenRecordingUploadedAt = screenRecording.UploadedAt;
                dto.ScreenRecordingExpiresAt = screenRecording.ExpiresAt;
                dto.ScreenRecordingStorageDeleted = screenRecording.IsStorageDeleted;
                dto.ScreenRecordingStorageDeletedAt = screenRecording.StorageDeletedAt;
            }
            else
            {
                dto.HasScreenRecording = false;
            }
        }

        private async Task FillAnswersAsync(SessionDetailDto dto, ExamSession session)
        {
            var questions = await _questionRepo.GetListAsync(question =>
                question.TestId == session.TestId);

            questions = questions
                .OrderBy(question => question.CreationTime)
                .ThenBy(question => question.Id)
                .ToList();

            if (!questions.Any())
            {
                return;
            }

            var questionIds = questions
                .Select(question => question.Id)
                .Distinct()
                .ToList();

            var answers = await _answerRepo.GetListAsync(answer =>
                answer.ExamSessionId == session.Id &&
                questionIds.Contains(answer.QuestionId));

            var options = await _optionRepo.GetListAsync(option =>
                questionIds.Contains(option.QuestionId));

            foreach (var question in questions)
            {
                var answer = answers.FirstOrDefault(answerItem =>
                    answerItem.QuestionId == question.Id);

                var questionOptions = options
                    .Where(option => option.QuestionId == question.Id)
                    .OrderBy(option => option.CreationTime)
                    .ThenBy(option => option.Id)
                    .ToList();

                List<string>? selectedOptionTexts = null;
                List<string>? correctOptionTexts = null;
                bool? isCorrect = null;

                if (question.Type == QuestionType.MultipleChoice)
                {
                    var selectedOptionIds = answer?.SelectedOptionIds ?? Array.Empty<Guid>();

                    var selectedOptionIdSet = selectedOptionIds.ToHashSet();

                    selectedOptionTexts = questionOptions
                        .Where(option => selectedOptionIdSet.Contains(option.Id))
                        .Select(option => option.Text)
                        .ToList();

                    correctOptionTexts = questionOptions
                        .Where(option => option.IsCorrect)
                        .Select(option => option.Text)
                        .ToList();

                    var correctIds = questionOptions
                        .Where(option => option.IsCorrect)
                        .Select(option => option.Id)
                        .OrderBy(id => id)
                        .ToArray();

                    var selectedIds = selectedOptionIds
                        .Distinct()
                        .OrderBy(id => id)
                        .ToArray();

                    isCorrect = correctIds.Length > 0 && correctIds.SequenceEqual(selectedIds);
                }

                dto.Answers.Add(new QuestionAnswerDetailDto
                {
                    QuestionId = question.Id,
                    QuestionText = question.Text,
                    QuestionType = question.Type.ToString(),
                    QuestionPoints = question.Points,

                    SelectedOptions = selectedOptionTexts,
                    CorrectOptions = correctOptionTexts,
                    IsCorrect = isCorrect,

                    TextAnswer = answer?.TextAnswer,

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

        private async Task FillProctoringEventsAsync(SessionDetailDto dto, Guid sessionId)
        {
            var events = await _proctoringEventRepo.GetListAsync(proctoringEvent =>
                proctoringEvent.ExamSessionId == sessionId);

            foreach (var proctoringEvent in events
                         .OrderBy(proctoringEvent => proctoringEvent.CreationTime)
                         .ThenBy(proctoringEvent => proctoringEvent.Id))
            {
                dto.ProctoringEvents.Add(new ProctoringEventDetailDto
                {
                    Id = proctoringEvent.Id,
                    Type = proctoringEvent.Type.ToString(),
                    Detail = proctoringEvent.Detail,
                    CreationTime = proctoringEvent.CreationTime
                });
            }
        }

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

            var recordings = await _recordingRepo.GetListAsync(recording =>
                recording.ExamSessionId == sessionId);

            foreach (var recording in recordings)
            {
                await _recordingRepo.DeleteAsync(recording, autoSave: false);
            }

            await _sessionRepo.DeleteAsync(session, autoSave: true);
        }
    }
}