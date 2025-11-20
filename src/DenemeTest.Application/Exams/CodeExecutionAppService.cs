using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DenemeTest.Application.Exams
{
    public class CodeExecutionAppService : ApplicationService, ICodeExecutionAppService
    {
        private readonly IRepository<Question, Guid> _questionRepo;
        private readonly IRepository<CodeTestCase, Guid> _testCaseRepo;
        private readonly ICodeRunner _codeRunner;

        public CodeExecutionAppService(
            IRepository<Question, Guid> questionRepo,
            IRepository<CodeTestCase, Guid> testCaseRepo,
            ICodeRunner codeRunner)
        {
            _questionRepo = questionRepo;
            _testCaseRepo = testCaseRepo;
            _codeRunner = codeRunner;
        }

        /// <summary>
        /// Kod + test-case input -> çalıştır -> output karşılaştır -> sonuçları döner.
        /// </summary>
        public async Task<RunCodeResultDto> RunAsync(RunCodeRequestDto input)
        {
            if (input.QuestionId == Guid.Empty)
                throw new UserFriendlyException("QuestionId boş olamaz.");

            if (string.IsNullOrWhiteSpace(input.Code))
            {
                return new RunCodeResultDto
                {
                    Success = false,
                    Error = "Kod boş gönderildi.",
                    ExitCode = 1
                };
            }

            // 1) Soru var mı?
            var question = await _questionRepo.GetAsync(input.QuestionId);
            if (question == null)
                throw new UserFriendlyException("Soru bulunamadı.");

            // 2) Test caseleri çek
            var testCases = await _testCaseRepo.GetListAsync(x => x.QuestionId == input.QuestionId);
            if (testCases.Count == 0)
            {
                return new RunCodeResultDto
                {
                    Success = false,
                    Error = "Bu soru için test-case tanımlı değil.",
                    ExitCode = 1
                };
            }

            var results = new List<TestCaseResultDto>();

            // 3) Her test-case için kodu çalıştır
            foreach (var tc in testCases)
            {
                var execResult = await _codeRunner.RunAsync(new CodeRunnerInput
                {
                    Code = input.Code,
                    Language = input.Language,
                    InputText = tc.Input
                });

                string std = (execResult.Output ?? string.Empty).Trim();
                string expected = (tc.ExpectedOutput ?? string.Empty).Trim();

                bool isSuccess =
                    execResult.ExitCode == 0 &&
                    string.Equals(std, expected, StringComparison.OrdinalIgnoreCase);

                results.Add(new TestCaseResultDto
                {
                    TestCaseId = tc.Id,
                    Input = tc.Input,
                    ExpectedOutput = tc.ExpectedOutput,
                    ActualOutput = execResult.Output,
                    Error = execResult.Error,
                    ExitCode = execResult.ExitCode,
                    IsSuccess = isSuccess
                });
            }

            // 4) Final başarı oranı
            int passed = results.Count(x => x.IsSuccess);
            int total = results.Count;

            // Eski UI Output alanını beslemek için kısa özet string üretelim
            var lines = results
                .Select((r, index) =>
                    $"#{index + 1} - {(r.IsSuccess ? "GEÇTİ" : "KALDI")} | Input: {r.Input}");

            var summaryText = $"Sonuç: {passed}/{total} test geçti.{Environment.NewLine}" +
                              string.Join(Environment.NewLine, lines);

            return new RunCodeResultDto
            {
                Success = passed == total,
                ExitCode = passed == total ? 0 : 1,
                TestCases = results,
                PassedCount = passed,
                TotalCount = total,
                Output = summaryText,
                Error = null
            };
        }
    }
}
