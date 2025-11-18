using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

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
            var inv = await _invRepo.FirstOrDefaultAsync(x => x.Token == token);
            if (inv == null)
                throw new BusinessException("Invitation:NotFound");

            if (inv.IsUsed)
                throw new BusinessException("Invitation:AlreadyUsed");

            if (inv.ExpireAt <= DateTime.UtcNow)
                throw new BusinessException("Invitation:Expired");

            // Kurucu startedAt istiyor -> UtcNow gönderiyoruz
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
                throw new UserFriendlyException("Oturum geçersiz.");

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
                    // Domain enumunu DTO enumuna çevir
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
                throw new UserFriendlyException("Oturum iptal edildi.");

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
                throw new UserFriendlyException("Oturum iptal.");

            var questions = await _questionRepo.GetListAsync(q => q.TestId == sess.TestId);
            var qIds = questions.Select(x => x.Id).ToList();
            var options = await _optionRepo.GetListAsync(o => qIds.Contains(o.QuestionId));
            var answers = await _answerRepo.GetListAsync(a => a.ExamSessionId == sessionId);

            int totalPoints = Math.Max(1, questions.Sum(q => q.Points));
            int earned = 0;

            foreach (var q in questions)
            {
                var ans = answers.FirstOrDefault(a => a.QuestionId == q.Id);

                if (q.Type == QuestionType.MultipleChoice)
                {
                    // Tüm doğru seçenekleri topla
                    var correct = options
                        .Where(o => o.QuestionId == q.Id && o.IsCorrect)
                        .Select(o => o.Id)
                        .OrderBy(x => x)
                        .ToArray();

                    // Adayın seçtikleri
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
                    (int score0_100, string _) = await _classic.ScoreAsync(q.Text, candText);
                    earned += (int)Math.Round(q.Points * (score0_100 / 100.0));
                }
                else if (q.Type == QuestionType.Coding)
                {
                    // Kod soruları için test-case tabanlı puanlama
                    var candCode = ans?.TextAnswer ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(candCode))
                    {
                        // Kod yoksa puan da yok
                        continue;
                    }

                    var testCases = await _codeTestRepo.GetListAsync(tc => tc.QuestionId == q.Id);
                    if (testCases == null || testCases.Count == 0)
                    {
                        // Test-case tanımlı değilse şimdilik puan vermeyelim
                        continue;
                    }

                    int totalWeight = testCases.Sum(tc => Math.Max(1, tc.Weight));
                    int passedWeight = 0;

                    foreach (var tc in testCases)
                    {
                        var runReq = new RunCodeRequestDto
                        {
                            SessionId = sess.Id,
                            QuestionId = q.Id,
                            Code = candCode,
                            Language = "csharp",
                            Input = tc.Input // 🔴 test-case input’u artık servise gidiyor
                        };

                        var runRes = await _codeExec.RunAsync(runReq);

                        if (!runRes.Success)
                        {
                            // Derleme/çalışma hatasında bu test-case başarısız sayılır
                            continue;
                        }

                        var actual = (runRes.Output ?? string.Empty).Trim();
                        var expected = (tc.ExpectedOutput ?? string.Empty).Trim();

                        if (actual == expected)
                        {
                            passedWeight += Math.Max(1, tc.Weight);
                        }
                    }

                    if (totalWeight > 0 && passedWeight > 0)
                    {
                        var ratio = passedWeight / (double)totalWeight;
                        earned += (int)Math.Round(q.Points * ratio);
                    }
                }
            }

            var finalScore = (int)Math.Round(100.0 * earned / totalPoints);

            // Eski skor varsa silip yeniden oluştur (entity set edilebilir değil)
            var existing = await _scoreRepo.FirstOrDefaultAsync(s => s.ExamSessionId == sessionId);
            if (existing != null)
            {
                await _scoreRepo.DeleteAsync(existing, autoSave: true);
            }

            var sc = new Score(
                GuidGenerator.Create(),
                sessionId,
                finalScore,
                "Auto-computed (MCQ + classic + coding)"
            );

            await _scoreRepo.InsertAsync(sc, autoSave: true);

            return finalScore;
        }

        // -------- helpers --------
        private static QuestionTypeDto MapToDto(QuestionType t) => t switch
        {
            QuestionType.MultipleChoice => QuestionTypeDto.MultipleChoice,
            QuestionType.Classic => QuestionTypeDto.Classic,
            QuestionType.Coding => QuestionTypeDto.Coding,
            _ => QuestionTypeDto.Classic
        };
    }
}
