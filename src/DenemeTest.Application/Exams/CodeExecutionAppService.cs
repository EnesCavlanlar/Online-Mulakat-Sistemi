using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        public async Task<RunCodeResultDto> RunAsync(RunCodeRequestDto input)
        {
            if (input.QuestionId == Guid.Empty)
            {
                throw new UserFriendlyException("QuestionId boş olamaz.");
            }

            if (string.IsNullOrWhiteSpace(input.Code))
            {
                return CreateFailResult(
                    "Kod boş gönderildi.",
                    "Kod boş gönderildi. Lütfen editöre C# kodunu yazıp tekrar çalıştır."
                );
            }

            var language = NormalizeLanguage(input.Language);

            if (language != "csharp")
            {
                return CreateFailResult(
                    "Desteklenmeyen dil.",
                    "Şu anda sadece C# kodu çalıştırılabilir."
                );
            }

            var question = await _questionRepo.GetAsync(input.QuestionId);

            if (question.Type != QuestionType.Coding)
            {
                return CreateFailResult(
                    "Bu soru kodlama sorusu değil.",
                    "Sadece kodlama sorularında kod çalıştırılabilir."
                );
            }

            var testCases = await _testCaseRepo.GetListAsync(x => x.QuestionId == input.QuestionId);

            testCases = testCases
                .OrderBy(x => x.CreationTime)
                .ThenBy(x => x.Id)
                .ToList();

            if (testCases.Count == 0)
            {
                return CreateFailResult(
                    "Bu soru için test-case tanımlı değil.",
                    "Admin panelinden bu kodlama sorusu için en az bir test-case eklenmeli."
                );
            }

            var results = new List<TestCaseResultDto>();

            foreach (var testCase in testCases)
            {
                var execResult = await RunSingleTestCaseAsync(input.Code, language, testCase);

                var actualOutput = NormalizeOutput(execResult.Output);
                var expectedOutput = NormalizeOutput(testCase.ExpectedOutput);

                var isSuccess =
                    execResult.ExitCode == 0 &&
                    IsOutputAccepted(expectedOutput, actualOutput);

                results.Add(new TestCaseResultDto
                {
                    TestCaseId = testCase.Id,

                    // Adaya gerçek input / expected output göstermiyoruz.
                    Input = string.IsNullOrWhiteSpace(testCase.Input) ? null : "Gizli input kullanıldı.",
                    ExpectedOutput = string.Empty,

                    ActualOutput = execResult.Output,
                    Error = execResult.Error,
                    ExitCode = execResult.ExitCode,
                    IsSuccess = isSuccess
                });

                if (execResult.ExitCode != 0)
                {
                    break;
                }
            }

            var passed = results.Count(x => x.IsSuccess);
            var total = testCases.Count;
            var allPassed = passed == total;

            var output = BuildCandidateFriendlyOutput(results, total);

            return new RunCodeResultDto
            {
                Success = allPassed,
                ExitCode = allPassed ? 0 : 1,
                TestCases = results,
                PassedCount = passed,
                TotalCount = total,
                Output = output,
                Error = allPassed ? null : BuildCandidateFriendlyError(results, total)
            };
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

        private static RunCodeResultDto CreateFailResult(string output, string error)
        {
            return new RunCodeResultDto
            {
                Success = false,
                Error = error,
                ExitCode = 1,
                PassedCount = 0,
                TotalCount = 0,
                Output = output,
                TestCases = new List<TestCaseResultDto>()
            };
        }

        private static string BuildCandidateFriendlyOutput(
            List<TestCaseResultDto> results,
            int totalTestCount)
        {
            var passed = results.Count(x => x.IsSuccess);

            var lines = new List<string>
            {
                passed == totalTestCount
                    ? $"✅ Başarılı: {passed}/{totalTestCount} test geçti."
                    : $"❌ Başarısız: {passed}/{totalTestCount} test geçti."
            };

            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];

                if (result.IsSuccess)
                {
                    lines.Add($"Test {i + 1}: Geçti");
                    continue;
                }

                lines.Add($"Test {i + 1}: Kaldı");

                if (!string.IsNullOrWhiteSpace(result.Input))
                {
                    lines.Add("Bu testte gizli input kullanıldı.");
                }

                if (!string.IsNullOrWhiteSpace(result.ActualOutput))
                {
                    lines.Add("Kodunun ürettiği çıktı:");
                    lines.Add(NormalizeOutput(result.ActualOutput));
                }

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    lines.Add("Hata:");
                    lines.Add(result.Error.Trim());
                }
                else
                {
                    lines.Add("Kodun çıktısı beklenen sonuçla eşleşmedi.");
                    lines.Add("İpucu: Sonucu ekrana yazdırdığından emin ol.");
                }
            }

            if (results.Count < totalTestCount)
            {
                lines.Add("İlk hata nedeniyle sonraki testler çalıştırılmadı.");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string? BuildCandidateFriendlyError(
            List<TestCaseResultDto> results,
            int totalTestCount)
        {
            var firstFailed = results.FirstOrDefault(x => !x.IsSuccess);

            if (firstFailed == null)
            {
                return $"Kod tüm testlerden geçemedi. Geçen test: {results.Count(x => x.IsSuccess)}/{totalTestCount}";
            }

            if (!string.IsNullOrWhiteSpace(firstFailed.Error))
            {
                return firstFailed.Error.Trim();
            }

            return "Kodun çıktısı beklenen sonuçla eşleşmedi.";
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

            var lines = value
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n')
                .Select(x => x.TrimEnd())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return string.Join(Environment.NewLine, lines).Trim();
        }
    }
}