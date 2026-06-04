using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace DenemeTest.Application.Exams
{
    public class ExamRunAppService : ApplicationService, IExamRunAppService
    {
        private readonly IRepository<ExamSession, Guid> _sessionRepo;
        private readonly IRepository<Test, Guid> _testRepo;
        private readonly IRepository<Question, Guid> _questionRepo;
        private readonly IRepository<QuestionOption, Guid> _optionRepo;
        private readonly IRepository<Answer, Guid> _answerRepo;
        private readonly IRepository<Score, Guid> _scoreRepo;
        private readonly IRepository<ExamInvitation, Guid> _invRepo;
        private readonly IRepository<CodeTestCase, Guid> _codeTestRepo;
        private readonly IClassicScoringProvider _classic;
        private readonly ICodeExecutionAppService _codeExec;

        public ExamRunAppService(
            IRepository<ExamSession, Guid> sessionRepo,
            IRepository<Test, Guid> testRepo,
            IRepository<Question, Guid> questionRepo,
            IRepository<QuestionOption, Guid> optionRepo,
            IRepository<Answer, Guid> answerRepo,
            IRepository<Score, Guid> scoreRepo,
            IRepository<ExamInvitation, Guid> invRepo,
            IRepository<CodeTestCase, Guid> codeTestRepo,
            IClassicScoringProvider classic,
            ICodeExecutionAppService codeExec)
        {
            _sessionRepo = sessionRepo;
            _testRepo = testRepo;
            _questionRepo = questionRepo;
            _optionRepo = optionRepo;
            _answerRepo = answerRepo;
            _scoreRepo = scoreRepo;
            _invRepo = invRepo;
            _codeTestRepo = codeTestRepo;
            _classic = classic;
            _codeExec = codeExec;
        }

        // -------------------- TOKEN İLE BAŞLAT --------------------

        public async Task<StartWithTokenResultDto> StartWithTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new BusinessException("Invitation:TokenEmpty");
            }

            var tokenHash = HashToken(token);

            var inv = await _invRepo.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
            if (inv == null)
            {
                throw new BusinessException("Invitation:NotFound");
            }

            if (inv.IsUsed)
            {
                throw new BusinessException("Invitation:AlreadyUsed");
            }

            if (inv.ExpireAt <= DateTime.UtcNow)
            {
                throw new BusinessException("Invitation:Expired");
            }

            var session = new ExamSession(
                id: GuidGenerator.Create(),
                testId: inv.TestId,
                candidateId: inv.CandidateId,
                startedAt: DateTime.UtcNow
            );

            await _sessionRepo.InsertAsync(session, autoSave: true);

            inv.MarkUsed();
            await _invRepo.UpdateAsync(inv, autoSave: true);

            var test = await _testRepo.GetAsync(inv.TestId);

            return new StartWithTokenResultDto
            {
                SessionId = session.Id,
                TestName = test.Name
            };
        }

        // -------------------- TESTİ KOŞUYA GETİR --------------------

        public async Task<TestRunDto> GetTestForRunAsync(Guid sessionId)
        {
            var sess = await _sessionRepo.FirstOrDefaultAsync(x => x.Id == sessionId);
            if (sess == null || sess.IsCancelled)
            {
                throw new UserFriendlyException("Oturum geçersiz.");
            }

            var test = await _testRepo.GetAsync(sess.TestId);

            var questions = await _questionRepo.GetListAsync(q => q.TestId == test.Id);
            var qIds = questions.Select(x => x.Id).ToList();
            var options = await _optionRepo.GetListAsync(o => qIds.Contains(o.QuestionId));

            return new TestRunDto
            {
                TestId = test.Id,
                TestName = test.Name,
                ShuffleQuestions = test.ShuffleQuestions,
                ShuffleOptions = test.ShuffleOptions,
                DurationMinutes = test.DurationMinutes,
                StartAt = test.StartAt,
                EndAt = test.EndAt,
                Questions = questions.Select(q => new QuestionRunDto
                {
                    Id = q.Id,
                    Text = q.Text,
                    Type = MapToDto(q.Type),
                    Points = q.Points,
                    Options = options
                        .Where(o => o.QuestionId == q.Id)
                        .Select(o => new QuestionOptionRunDto
                        {
                            Id = o.Id,
                            Text = o.Text
                        })
                        .ToList()
                }).ToList()
            };
        }

        // -------------------- CEVAP KAYDET --------------------

        public async Task SubmitAnswerAsync(SubmitAnswerDto input)
        {
            var sess = await _sessionRepo.GetAsync(input.SessionId);

            if (sess.IsCancelled)
            {
                throw new UserFriendlyException("Oturum iptal edildi.");
            }

            var exist = await _answerRepo.FirstOrDefaultAsync(
                a => a.ExamSessionId == input.SessionId && a.QuestionId == input.QuestionId);

            if (exist == null)
            {
                exist = new Answer(
                    GuidGenerator.Create(),
                    input.SessionId,
                    input.QuestionId,
                    input.TextAnswer,
                    input.SelectedOptionIds
                );

                await _answerRepo.InsertAsync(exist, autoSave: true);
            }
            else
            {
                exist.UpdateText(input.TextAnswer);
                exist.UpdateOptions(input.SelectedOptionIds);

                await _answerRepo.UpdateAsync(exist, autoSave: true);
            }
        }

        // -------------------- PUAN HESAPLA --------------------

        public async Task<int> ComputeAndSaveScoreAsync(Guid sessionId)
        {
            var sess = await _sessionRepo.GetAsync(sessionId);

            if (sess.IsCancelled)
            {
                throw new UserFriendlyException("Oturum iptal.");
            }

            var questions = await _questionRepo.GetListAsync(q => q.TestId == sess.TestId);
            var qIds = questions.Select(q => q.Id).ToList();

            var options = await _optionRepo.GetListAsync(o => qIds.Contains(o.QuestionId));
            var answers = await _answerRepo.GetListAsync(a => a.ExamSessionId == sessionId);

            var totalPoints = Math.Max(1, questions.Sum(q => q.Points));
            var earned = 0;

            foreach (var q in questions)
            {
                var ans = answers.FirstOrDefault(a => a.QuestionId == q.Id);

                if (q.Type == QuestionType.MultipleChoice)
                {
                    var correct = options
                        .Where(o => o.QuestionId == q.Id && o.IsCorrect)
                        .Select(o => o.Id)
                        .OrderBy(x => x)
                        .ToArray();

                    var chosen = (ans?.SelectedOptionIds ?? Array.Empty<Guid>())
                        .OrderBy(x => x)
                        .ToArray();

                    if (correct.SequenceEqual(chosen))
                    {
                        earned += q.Points;
                    }
                }
                else if (q.Type == QuestionType.Classic)
                {
                    var candText = ans?.TextAnswer ?? string.Empty;
                    var (score0_100, _) = await _classic.ScoreAsync(q.Text, candText);

                    earned += (int)Math.Round(q.Points * (score0_100 / 100.0));
                }
                else if (q.Type == QuestionType.Coding)
                {
                    earned += await ScoreCodingQuestionAsync(q, ans, sessionId);
                }
            }

            var finalScore = (int)Math.Round(100.0 * earned / totalPoints);

            var existing = await _scoreRepo.FirstOrDefaultAsync(s => s.ExamSessionId == sessionId);
            if (existing != null)
            {
                await _scoreRepo.DeleteAsync(existing, autoSave: true);
            }

            var score = new Score(
                GuidGenerator.Create(),
                sessionId,
                finalScore,
                "Auto-computed (MCQ + classic + coding)"
            );

            await _scoreRepo.InsertAsync(score, autoSave: true);

            return finalScore;
        }

        // -------------------- CODING PUANLAMA YARDIMCI --------------------

        private async Task<int> ScoreCodingQuestionAsync(Question question, Answer? answer, Guid sessionId)
        {
            var code = answer?.TextAnswer;

            if (string.IsNullOrWhiteSpace(code))
            {
                return 0;
            }

            var testCases = await _codeTestRepo.GetListAsync(x => x.QuestionId == question.Id);

            if (testCases.Count == 0)
            {
                return 0;
            }

            var runResult = await _codeExec.RunAsync(new RunCodeRequestDto
            {
                SessionId = sessionId,
                QuestionId = question.Id,
                Code = code,
                Language = "csharp"
            });

            if (runResult.TestCases == null || runResult.TestCases.Count == 0)
            {
                return 0;
            }

            var totalWeight = testCases.Sum(x => Math.Max(1, x.Weight));
            if (totalWeight <= 0)
            {
                return 0;
            }

            var passedWeight = 0;

            foreach (var result in runResult.TestCases)
            {
                if (!result.IsSuccess)
                {
                    continue;
                }

                var matchedTestCase = testCases.FirstOrDefault(x => x.Id == result.TestCaseId);
                if (matchedTestCase == null)
                {
                    continue;
                }

                passedWeight += Math.Max(1, matchedTestCase.Weight);
            }

            var ratio = passedWeight / (double)totalWeight;
            var earnedPoint = (int)Math.Round(question.Points * ratio);

            return earnedPoint;
        }

        private static string HashToken(string rawToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // -------------------- HELPERS --------------------

        private static QuestionTypeDto MapToDto(QuestionType type)
        {
            return type switch
            {
                QuestionType.MultipleChoice => QuestionTypeDto.MultipleChoice,
                QuestionType.Classic => QuestionTypeDto.Classic,
                QuestionType.Coding => QuestionTypeDto.Coding,
                _ => QuestionTypeDto.Classic
            };
        }
    }
}