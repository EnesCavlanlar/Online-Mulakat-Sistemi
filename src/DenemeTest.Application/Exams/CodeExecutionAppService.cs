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
        /// Adayın gönderdiği C# kodunu ilgili sorunun test case'leri ile çalıştırır.
        /// Her test case için output karşılaştırması yapar ve sonucu döner.
        /// </summary>
        public async Task<RunCodeResultDto> RunAsync(RunCodeRequestDto input)
        {
            if (input.QuestionId == Guid.Empty)
            {
                throw new UserFriendlyException("QuestionId boş olamaz.");
            }

            if (string.IsNullOrWhiteSpace(input.Code))
            {
                return new RunCodeResultDto
                {
                    Success = false,
                    Error = "Kod boş gönderildi.",
                    ExitCode = 1,
                    PassedCount = 0,
                    TotalCount = 0,
                    Output = "Kod boş gönderildi."
                };
            }

            var language = NormalizeLanguage(input.Language);

            if (language != "csharp")
            {
                return new RunCodeResultDto
                {
                    Success = false,
                    Error = "Şu anda sadece C# kodu çalıştırılabilir.",
                    ExitCode = 1,
                    PassedCount = 0,
                    TotalCount = 0,
                    Output = "Desteklenmeyen dil. Sadece C# desteklenmektedir."
                };
            }

            await _questionRepo.GetAsync(input.QuestionId);

            var testCases = await _testCaseRepo.GetListAsync(x => x.QuestionId == input.QuestionId);

            testCases = testCases
                .OrderBy(x => x.CreationTime)
                .ThenBy(x => x.Id)
                .ToList();

            if (testCases.Count == 0)
            {
                return new RunCodeResultDto
                {
                    Success = false,
                    Error = "Bu soru için test-case tanımlı değil.",
                    ExitCode = 1,
                    PassedCount = 0,
                    TotalCount = 0,
                    Output = "Bu soru için test-case tanımlı değil."
                };
            }

            var results = new List<TestCaseResultDto>();

            foreach (var testCase in testCases)
            {
                CodeRunnerResult execResult;

                try
                {
                    execResult = await _codeRunner.RunAsync(new CodeRunnerInput
                    {
                        Code = input.Code,
                        Language = language,
                        InputText = testCase.Input,
                        TimeoutMilliseconds = 3000
                    });
                }
                catch (Exception ex)
                {
                    execResult = new CodeRunnerResult
                    {
                        ExitCode = 1,
                        Output = string.Empty,
                        Error = "Kod çalıştırılırken beklenmeyen bir hata oluştu: " + ex.Message
                    };
                }

                var actualOutput = NormalizeOutput(execResult.Output);
                var expectedOutput = NormalizeOutput(testCase.ExpectedOutput);

                var isSuccess =
                    execResult.ExitCode == 0 &&
                    string.Equals(actualOutput, expectedOutput, StringComparison.Ordinal);

                results.Add(new TestCaseResultDto
                {
                    TestCaseId = testCase.Id,
                    Input = testCase.Input,
                    ExpectedOutput = testCase.ExpectedOutput,
                    ActualOutput = execResult.Output,
                    Error = execResult.Error,
                    ExitCode = execResult.ExitCode,
                    IsSuccess = isSuccess
                });
            }

            var passed = results.Count(x => x.IsSuccess);
            var total = results.Count;
            var allPassed = passed == total;

            var summaryLines = results.Select((result, index) =>
            {
                var status = result.IsSuccess ? "GEÇTİ" : "KALDI";
                var errorPart = string.IsNullOrWhiteSpace(result.Error)
                    ? string.Empty
                    : $" | Hata: {result.Error}";

                return $"#{index + 1} - {status} | Input: {result.Input} | Beklenen: {result.ExpectedOutput} | Gelen: {result.ActualOutput}{errorPart}";
            });

            var summaryText =
                $"Sonuç: {passed}/{total} test geçti." +
                Environment.NewLine +
                string.Join(Environment.NewLine, summaryLines);

            return new RunCodeResultDto
            {
                Success = allPassed,
                ExitCode = allPassed ? 0 : 1,
                TestCases = results,
                PassedCount = passed,
                TotalCount = total,
                Output = summaryText,
                Error = allPassed ? null : "Kod bazı test case'lerden geçemedi."
            };
        }

        private static string NormalizeLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return "csharp";
            }

            var normalized = language.Trim().ToLowerInvariant();

            return normalized switch
            {
                "c#" => "csharp",
                "cs" => "csharp",
                "csharp" => "csharp",
                _ => normalized
            };
        }

        private static string NormalizeOutput(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();
        }
    }
}