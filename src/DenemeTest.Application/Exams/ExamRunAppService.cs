using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;                 // ToDto() uzantısı ve DTO’lar
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
        private readonly IClassicScoringProvider _classic;

        public ExamRunAppService(
            IRepository<ExamSession, Guid> sessionRepo,
            IRepository<Test, Guid> testRepo,
            IRepository<Question, Guid> questionRepo,
            IRepository<QuestionOption, Guid> optionRepo,
            IRepository<Answer, Guid> answerRepo,
            IRepository<Score, Guid> scoreRepo,
            IClassicScoringProvider classic)
        {
            _sessionRepo = sessionRepo;
            _testRepo = testRepo;
            _questionRepo = questionRepo;
            _optionRepo = optionRepo;
            _answerRepo = answerRepo;
            _scoreRepo = scoreRepo;
            _classic = classic;
        }

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
                Questions = questions.Select(q => new QuestionRunDto
                {
                    Id = q.Id,
                    Text = q.Text,
                    // !!! Domain enum -> DTO enum
                    Type = q.Type.ToDto(),
                    Points = q.Points,
                    Options = options
                        .Where(o => o.QuestionId == q.Id)
                        .Select(o => new QuestionOptionRunDto { Id = o.Id, Text = o.Text })
                        .ToList()
                }).ToList()
            };
        }

        // Tam nitelikli ad da çalışır; istersen sadece SubmitAnswerDto kullanabilirsin.
        public async Task SubmitAnswerAsync(DenemeTest.Exams.Dtos.SubmitAnswerDto input)
        {
            var sess = await _sessionRepo.GetAsync(input.SessionId);
            if (sess.IsCancelled)
                throw new UserFriendlyException("Oturum iptal edildi.");

            var exist = await _answerRepo.FirstOrDefaultAsync(
                a => a.ExamSessionId == input.SessionId && a.QuestionId == input.QuestionId);

            if (exist == null)
            {
                exist = new Answer(GuidGenerator.Create(), input.SessionId, input.QuestionId,
                                   input.TextAnswer, input.SelectedOptionIds);
                await _answerRepo.InsertAsync(exist, autoSave: true);
            }
            else
            {
                exist.UpdateText(input.TextAnswer);
                exist.UpdateOptions(input.SelectedOptionIds);
                await _answerRepo.UpdateAsync(exist, autoSave: true);
            }
        }

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
                    var correct = options.Where(o => o.QuestionId == q.Id && o.IsCorrect)
                                         .Select(o => o.Id).OrderBy(x => x).ToArray();
                    var chosen = (ans?.SelectedOptionIds ?? Array.Empty<Guid>())
                                 .OrderBy(x => x).ToArray();

                    if (correct.SequenceEqual(chosen))
                        earned += q.Points;
                }
                else
                {
                    var candText = ans?.TextAnswer ?? "";
                    (int score0_100, string _) = await _classic.ScoreAsync(q.Text, candText);
                    earned += (int)Math.Round(q.Points * (score0_100 / 100.0));
                }
            }

            var finalScore = (int)Math.Round(100.0 * earned / totalPoints);
            var sc = new Score(GuidGenerator.Create(), sessionId, finalScore,
                               "Auto-computed (MCQ + stub classic)");
            await _scoreRepo.InsertAsync(sc, autoSave: true);
            return finalScore;
        }
    }

    public interface IExamRunAppService
    {
        Task<TestRunDto> GetTestForRunAsync(Guid sessionId);
        Task SubmitAnswerAsync(DenemeTest.Exams.Dtos.SubmitAnswerDto input);
        Task<int> ComputeAndSaveScoreAsync(Guid sessionId);
    }
}
// Add the following extension method to convert QuestionType to QuestionTypeDto.

public static class QuestionTypeExtensions
{
    public static QuestionTypeDto ToDto(this QuestionType questionType)
    {
        return questionType switch
        {
            QuestionType.MultipleChoice => QuestionTypeDto.MultipleChoice,
            QuestionType.Classic => QuestionTypeDto.Classic,
            _ => throw new ArgumentOutOfRangeException(nameof(questionType), questionType, null)
        };
    }
}
// Ensure the namespace containing the extension method is included at the top of the file.

public class SubmitAnswerDto
{
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? TextAnswer { get; set; }
    public Guid SessionId { get; set; } // Added property
    public Guid[]? SelectedOptionIds { get; set; } // Fix: Added missing property
}
