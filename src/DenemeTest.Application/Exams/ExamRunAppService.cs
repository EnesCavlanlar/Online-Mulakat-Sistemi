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

            var normalizedToken = token.Trim();
            var tokenHash = HashToken(normalizedToken);

            var inv = await _invRepo.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
            if (inv == null)
            {
                throw new BusinessException("Invitation:NotFound");
            }

            if (inv.IsUsed)
            {
                throw new BusinessException("Invitation:AlreadyUsed");
            }

            var now = Clock.Now;

            if (inv.ExpireAt <= now)
            {
                throw new BusinessException("Invitation:Expired");
            }

            var test = await _testRepo.GetAsync(inv.TestId);

            if (test.StartAt.HasValue && now < test.StartAt.Value)
            {
                throw new UserFriendlyException("Sınav henüz başlamadı.");
            }

            if (test.EndAt.HasValue && now > test.EndAt.Value)
            {
                throw new UserFriendlyException("Sınavın bitiş zamanı geçmiş.");
            }

            var alreadyActiveSession = await _sessionRepo.FirstOrDefaultAsync(session =>
                session.TestId == inv.TestId &&
                session.CandidateId == inv.CandidateId &&
                !session.IsCancelled &&
                session.FinishedAt == null
            );

            if (alreadyActiveSession != null)
            {
                inv.MarkUsed();
                await _invRepo.UpdateAsync(inv, autoSave: true);

                throw new UserFriendlyException("Bu test için zaten aktif bir oturum var. Aynı davetle ikinci kez giriş yapılamaz.");
            }

            var session = new ExamSession(
                id: GuidGenerator.Create(),
                testId: inv.TestId,
                candidateId: inv.CandidateId,
                startedAt: now
            );

            await _sessionRepo.InsertAsync(session, autoSave: true);

            inv.MarkUsed();
            await _invRepo.UpdateAsync(inv, autoSave: true);

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

            if (sess.FinishedAt != null)
            {
                throw new UserFriendlyException("Bu oturum daha önce tamamlanmış.");
            }

            var test = await _testRepo.GetAsync(sess.TestId);

            var questions = await _questionRepo.GetListAsync(q => q.TestId == test.Id);

            questions = questions
                .OrderBy(q => q.CreationTime)
                .ThenBy(q => q.Id)
                .ToList();

            if (test.ShuffleQuestions)
            {
                questions = StableShuffle(
                    questions,
                    $"{sessionId:N}:questions"
                );
            }

            var qIds = questions.Select(x => x.Id).ToList();

            var options = await _optionRepo.GetListAsync(o => qIds.Contains(o.QuestionId));

            options = options
                .OrderBy(o => o.CreationTime)
                .ThenBy(o => o.Id)
                .ToList();

            return new TestRunDto
            {
                TestId = test.Id,
                TestName = test.Name,
                ShuffleQuestions = test.ShuffleQuestions,
                ShuffleOptions = test.ShuffleOptions,
                DurationMinutes = test.DurationMinutes,
                StartAt = test.StartAt,
                EndAt = test.EndAt,
                Questions = questions.Select(q =>
                {
                    var questionOptions = options
                        .Where(o => o.QuestionId == q.Id)
                        .ToList();

                    if (test.ShuffleOptions)
                    {
                        questionOptions = StableShuffle(
                            questionOptions,
                            $"{sessionId:N}:{q.Id:N}:options"
                        );
                    }

                    return new QuestionRunDto
                    {
                        Id = q.Id,
                        Text = q.Text,
                        Type = MapToDto(q.Type),
                        Points = q.Points,
                        Options = questionOptions
                            .Select(o => new QuestionOptionRunDto
                            {
                                Id = o.Id,
                                Text = o.Text
                            })
                            .ToList()
                    };
                }).ToList()
            };
        }

        // -------------------- CEVAP KAYDET --------------------

        public async Task SubmitAnswerAsync(SubmitAnswerDto input)
        {
            if (input.SessionId == Guid.Empty)
            {
                throw new UserFriendlyException("Oturum bilgisi boş olamaz.");
            }

            if (input.QuestionId == Guid.Empty)
            {
                throw new UserFriendlyException("Soru bilgisi boş olamaz.");
            }

            var sess = await _sessionRepo.GetAsync(input.SessionId);

            if (sess.IsCancelled)
            {
                throw new UserFriendlyException("Oturum iptal edildi.");
            }

            if (sess.FinishedAt != null)
            {
                throw new UserFriendlyException("Tamamlanmış sınava cevap kaydedilemez.");
            }

            var question = await _questionRepo.GetAsync(input.QuestionId);

            if (question.TestId != sess.TestId)
            {
                throw new UserFriendlyException("Bu soru ilgili sınava ait değil.");
            }

            Guid[]? selectedOptionIds = null;

            if (question.Type == QuestionType.MultipleChoice)
            {
                selectedOptionIds = input.SelectedOptionIds?
                    .Where(x => x != Guid.Empty)
                    .Distinct()
                    .ToArray() ?? Array.Empty<Guid>();

                if (selectedOptionIds.Length > 0)
                {
                    var validOptionIds = await _optionRepo.GetListAsync(o =>
                        o.QuestionId == question.Id &&
                        selectedOptionIds.Contains(o.Id)
                    );

                    var validIdSet = validOptionIds
                        .Select(x => x.Id)
                        .ToHashSet();

                    selectedOptionIds = selectedOptionIds
                        .Where(validIdSet.Contains)
                        .ToArray();
                }
            }

            var textAnswer = question.Type == QuestionType.MultipleChoice
                ? null
                : input.TextAnswer;

            var exist = await _answerRepo.FirstOrDefaultAsync(
                a => a.ExamSessionId == input.SessionId && a.QuestionId == input.QuestionId);

            if (exist == null)
            {
                exist = new Answer(
                    GuidGenerator.Create(),
                    input.SessionId,
                    input.QuestionId,
                    textAnswer,
                    selectedOptionIds
                );

                await _answerRepo.InsertAsync(exist, autoSave: true);
            }
            else
            {
                exist.UpdateText(textAnswer);
                exist.UpdateOptions(selectedOptionIds);

                await _answerRepo.UpdateAsync(exist, autoSave: true);
            }
        }

        // -------------------- PUAN HESAPLA --------------------

        public async Task<int> ComputeAndSaveScoreAsync(Guid sessionId)
        {
            var sess = await _sessionRepo.GetAsync(sessionId);

            /*
             * İptal edilmiş oturumlarda da puan hesaplamaya izin veriyoruz.
             * Çünkü politika ihlali / kullanıcı iptali anında adayın o ana kadar
             * aldığı puanı rapora yazmak istiyoruz.
             */

            var questions = await _questionRepo.GetListAsync(q => q.TestId == sess.TestId);

            questions = questions
                .OrderBy(q => q.CreationTime)
                .ThenBy(q => q.Id)
                .ToList();

            var qIds = questions.Select(q => q.Id).ToList();

            var options = await _optionRepo.GetListAsync(o => qIds.Contains(o.QuestionId));

            var answers = await _answerRepo.GetListAsync(a => a.ExamSessionId == sessionId);

            var totalPoints = Math.Max(1, questions.Sum(q => Math.Max(0, q.Points)));
            var earned = 0;

            foreach (var q in questions)
            {
                var ans = answers.FirstOrDefault(a => a.QuestionId == q.Id);

                if (q.Points <= 0)
                {
                    continue;
                }

                if (q.Type == QuestionType.MultipleChoice)
                {
                    var correct = options
                        .Where(o => o.QuestionId == q.Id && o.IsCorrect)
                        .Select(o => o.Id)
                        .OrderBy(x => x)
                        .ToArray();

                    var chosen = (ans?.SelectedOptionIds ?? Array.Empty<Guid>())
                        .Distinct()
                        .OrderBy(x => x)
                        .ToArray();

                    if (correct.Length > 0 && correct.SequenceEqual(chosen))
                    {
                        earned += q.Points;
                    }
                }
                else if (q.Type == QuestionType.Classic)
                {
                    var candText = ans?.TextAnswer ?? string.Empty;
                    var (score0_100, _) = await _classic.ScoreAsync(q.Text, candText);

                    score0_100 = NormalizeScore(score0_100);

                    earned += (int)Math.Round(q.Points * (score0_100 / 100.0));
                }
                else if (q.Type == QuestionType.Coding)
                {
                    earned += await ScoreCodingQuestionAsync(q, ans, sessionId);
                }
            }

            var finalScore = NormalizeScore((int)Math.Round(100.0 * earned / totalPoints));

            var existing = await _scoreRepo.FirstOrDefaultAsync(s => s.ExamSessionId == sessionId);
            if (existing != null)
            {
                await _scoreRepo.DeleteAsync(existing, autoSave: true);
            }

            var note = sess.IsCancelled
                ? "Auto-computed for cancelled session (MCQ + classic + coding)"
                : "Auto-computed (MCQ + classic + coding)";

            var score = new Score(
                GuidGenerator.Create(),
                sessionId,
                finalScore,
                note
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

            testCases = testCases
                .OrderBy(x => x.CreationTime)
                .ThenBy(x => x.Id)
                .ToList();

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

            if (earnedPoint < 0)
            {
                return 0;
            }

            if (earnedPoint > question.Points)
            {
                return question.Points;
            }

            return earnedPoint;
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

        private static List<T> StableShuffle<T>(IEnumerable<T> source, string seed)
        {
            var list = source.ToList();

            if (list.Count <= 1)
            {
                return list;
            }

            var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            var seedValue = BitConverter.ToInt32(seedBytes, 0);
            var random = new Random(seedValue);

            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list;
        }
    }
}