using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
    private readonly IRepository<Test, Guid> _testRepo;
    private readonly IRepository<ExamSession, Guid> _sessionRepo;
    private readonly IRepository<ProctoringEvent, Guid> _eventRepo;
    private readonly IRepository<Score, Guid> _scoreRepo;
    private readonly IRepository<Answer, Guid> _answerRepo;
    private readonly IRepository<Question, Guid> _questionRepo;
    private readonly IRepository<CodeTestCase, Guid> _testCaseRepo;
    private readonly IRepository<CodeReview, Guid> _codeReviewRepo;
    private readonly ICodeRunner _codeRunner;
    private readonly ICodeLlmReviewService _llmReviewService;
    private readonly IConfiguration _config;

    public ExamSessionAppService(
        IRepository<ExamInvitation, Guid> invRepo,
        IRepository<Test, Guid> testRepo,
        IRepository<ExamSession, Guid> sessionRepo,
        IRepository<ProctoringEvent, Guid> eventRepo,
        IRepository<Score, Guid> scoreRepo,
        IRepository<Answer, Guid> answerRepo,
        IRepository<Question, Guid> questionRepo,
        IRepository<CodeTestCase, Guid> testCaseRepo,
        IRepository<CodeReview, Guid> codeReviewRepo,
        ICodeRunner codeRunner,
        ICodeLlmReviewService llmReviewService,
        IConfiguration config)
    {
        _invRepo = invRepo;
        _testRepo = testRepo;
        _sessionRepo = sessionRepo;
        _eventRepo = eventRepo;
        _scoreRepo = scoreRepo;
        _answerRepo = answerRepo;
        _questionRepo = questionRepo;
        _testCaseRepo = testCaseRepo;
        _codeReviewRepo = codeReviewRepo;
        _codeRunner = codeRunner;
        _llmReviewService = llmReviewService;
        _config = config;
    }

    [UnitOfWork]
    public async Task<StartByTokenResultDto> StartByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UserFriendlyException("Davet tokenı boş olamaz.");
        }

        var normalizedToken = token.Trim();
        var tokenHash = HashToken(normalizedToken);

        var invitation = await _invRepo.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        if (invitation == null)
        {
            throw new UserFriendlyException("Davet bulunamadı.");
        }

        if (invitation.IsUsed)
        {
            throw new UserFriendlyException("Bu davet zaten kullanılmış. Aynı link ile tekrar sınava girilemez.");
        }

        var now = Clock.Now;

        if (invitation.ExpireAt <= now)
        {
            throw new UserFriendlyException("Davetin süresi geçmiş.");
        }

        var test = await _testRepo.GetAsync(invitation.TestId);

        if (test.StartAt.HasValue && now < test.StartAt.Value)
        {
            throw new UserFriendlyException("Sınav henüz başlamadı.");
        }

        if (test.EndAt.HasValue && now > test.EndAt.Value)
        {
            throw new UserFriendlyException("Sınavın bitiş zamanı geçmiş.");
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
            now
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

    [UnitOfWork]
    public async Task RecordEventAsync(Guid sessionId, ProctoringEventTypeDto type, string? detail)
    {
        var session = await _sessionRepo.GetAsync(sessionId);

        if (session.IsCancelled || session.FinishedAt != null)
        {
            return;
        }

        session.RegisterViolation();

        var maxViolationCount = GetMaxViolations();

        var normalizedDetail = string.IsNullOrWhiteSpace(detail)
            ? "-"
            : detail.Trim();

        normalizedDetail =
            $"{normalizedDetail} | İhlal sayısı: {session.ViolationCount}/{maxViolationCount}";

        if (session.ViolationCount >= maxViolationCount)
        {
            normalizedDetail =
                $"{normalizedDetail} | Proctoring limiti aşıldı.";
        }

        var proctoringEvent = new ProctoringEvent(
            GuidGenerator.Create(),
            sessionId,
            MapToDomainProctoringEventType(type),
            normalizedDetail
        );

        await _eventRepo.InsertAsync(proctoringEvent, autoSave: true);

        /*
         * Burada session.Cancel(...) çağırmıyoruz.
         *
         * Politika ihlali geldiğinde Runner.razor tarafı önce mevcut cevabı kaydediyor,
         * sonra puanı hesaplıyor, sonra CancelAsync çağırıyor.
         *
         * Burada direkt iptal edersek puan hesaplama akışı bozulabilir.
         */
        await _sessionRepo.UpdateAsync(session, autoSave: true);
    }

    [UnitOfWork]
    public async Task CancelAsync(Guid sessionId, string reason)
    {
        var session = await _sessionRepo.GetAsync(sessionId);

        if (session.FinishedAt != null)
        {
            return;
        }

        if (session.IsCancelled)
        {
            return;
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "Sınav iptal edildi."
            : reason.Trim();

        session.Cancel(normalizedReason);

        await _sessionRepo.UpdateAsync(session, autoSave: true);

        await GenerateCodeReviewsSafelyAsync(sessionId);
    }

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

        if (scoreValue.HasValue)
        {
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

        await GenerateCodeReviewsSafelyAsync(sessionId);
    }

    private async Task GenerateCodeReviewsSafelyAsync(Guid sessionId)
    {
        try
        {
            var llmEnabled = _config.GetValue<bool>("LlmReview:Enabled");
            if (!llmEnabled)
            {
                return;
            }

            var answers = await _answerRepo.GetListAsync(x => x.ExamSessionId == sessionId);

            if (answers.Count == 0)
            {
                return;
            }

            var questionIds = answers
                .Select(x => x.QuestionId)
                .Distinct()
                .ToList();

            var questions = await _questionRepo.GetListAsync(x => questionIds.Contains(x.Id));

            foreach (var answer in answers)
            {
                var question = questions.FirstOrDefault(x => x.Id == answer.QuestionId);
                if (question == null)
                {
                    continue;
                }

                if (question.Type != QuestionType.Coding)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(answer.TextAnswer))
                {
                    continue;
                }

                await GenerateSingleCodeReviewAsync(
                    sessionId,
                    question,
                    answer.TextAnswer
                );
            }
        }
        catch
        {
            // LLM analizi sınav bitirme/iptal akışını bozmasın.
        }
    }

    private async Task GenerateSingleCodeReviewAsync(
        Guid sessionId,
        Question question,
        string code)
    {
        var testCases = await _testCaseRepo.GetListAsync(x => x.QuestionId == question.Id);

        testCases = testCases
            .OrderBy(x => x.CreationTime)
            .ThenBy(x => x.Id)
            .ToList();

        if (testCases.Count == 0)
        {
            return;
        }

        var passedCount = 0;
        var totalCount = testCases.Count;

        foreach (var testCase in testCases)
        {
            var execResult = await RunSingleTestCaseAsync(code, "csharp", testCase);

            var actualOutput = NormalizeOutput(execResult.Output);
            var expectedOutput = NormalizeOutput(testCase.ExpectedOutput);

            var passed =
                execResult.ExitCode == 0 &&
                IsOutputAccepted(expectedOutput, actualOutput);

            if (passed)
            {
                passedCount++;
                continue;
            }

            break;
        }

        var allPassed = passedCount == totalCount;

        var llmReview = await _llmReviewService.ReviewAsync(new CodeLlmReviewInput
        {
            QuestionText = question.Text,
            Code = code,
            TestsPassed = allPassed,
            PassedCount = passedCount,
            TotalCount = totalCount
        });

        if (!llmReview.Enabled)
        {
            return;
        }

        var provider = _config["LlmReview:Provider"];
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = "OpenRouter";
        }

        var flags = llmReview.Flags == null || llmReview.Flags.Length == 0
            ? string.Empty
            : string.Join(", ", llmReview.Flags);

        var summary = llmReview.Available
            ? llmReview.Summary
            : "LLM analizi alınamadı: " + llmReview.Summary;

        var existingReview = await _codeReviewRepo.FirstOrDefaultAsync(x =>
            x.ExamSessionId == sessionId &&
            x.QuestionId == question.Id
        );

        if (existingReview != null)
        {
            existingReview.Update(
                allPassed,
                passedCount,
                totalCount,
                llmReview.IsSuspicious,
                llmReview.QualityScore,
                summary,
                flags,
                provider
            );

            await _codeReviewRepo.UpdateAsync(existingReview, autoSave: true);
            return;
        }

        var codeReview = new CodeReview(
            GuidGenerator.Create(),
            sessionId,
            question.Id,
            allPassed,
            passedCount,
            totalCount,
            llmReview.IsSuspicious,
            llmReview.QualityScore,
            summary,
            flags,
            provider
        );

        await _codeReviewRepo.InsertAsync(codeReview, autoSave: true);
    }

    private async Task<CodeRunnerResult> RunSingleTestCaseAsync(
        string code,
        string language,
        CodeTestCase testCase)
    {
        try
        {
            return await _codeRunner.RunAsync(new CodeRunnerInput
            {
                Code = code,
                Language = language,
                InputText = testCase.Input,
                TimeoutMilliseconds = 3000
            });
        }
        catch (Exception ex)
        {
            return new CodeRunnerResult
            {
                ExitCode = 1,
                Output = string.Empty,
                Error = "Kod çalıştırılırken beklenmeyen bir hata oluştu: " + ex.Message
            };
        }
    }

    private static bool IsOutputAccepted(string expectedOutput, string actualOutput)
    {
        var expected = NormalizeOutput(expectedOutput);
        var actual = NormalizeOutput(actualOutput);

        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsSingleNumber(expected))
        {
            var expectedNumber = ExtractNumbers(expected).LastOrDefault();
            var actualNumbers = ExtractNumbers(actual);

            if (!string.IsNullOrWhiteSpace(expectedNumber) &&
                actualNumbers.Count > 0 &&
                string.Equals(actualNumbers.Last(), expectedNumber, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSingleNumber(string value)
    {
        var normalized = NormalizeNumber(value);
        var numbers = ExtractNumbers(normalized);

        return numbers.Count == 1 &&
               string.Equals(numbers[0], normalized.Trim(), StringComparison.Ordinal);
    }

    private static List<string> ExtractNumbers(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return Regex.Matches(value, @"-?\d+([.,]\d+)?")
            .Select(x => NormalizeNumber(x.Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static string NormalizeNumber(string value)
    {
        return value
            .Trim()
            .Replace(",", ".");
    }

    private static string NormalizeOutput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lines = value
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(x => x.TrimEnd())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return string.Join(Environment.NewLine, lines).Trim();
    }

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

    private static ProctoringEventType MapToDomainProctoringEventType(ProctoringEventTypeDto type)
    {
        return (ProctoringEventType)(int)type;
    }
}