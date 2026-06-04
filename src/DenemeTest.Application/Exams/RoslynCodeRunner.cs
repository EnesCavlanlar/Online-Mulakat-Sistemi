using DenemeTest.Application.Exams;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DenemeTest.Exams
{
    public class RoslynCodeRunner : ICodeRunner
    {
        private static readonly SemaphoreSlim ConsoleLock = new(1, 1);

        public async Task<CodeRunnerResult> RunAsync(CodeRunnerInput input)
        {
            var language = NormalizeLanguage(input.Language);

            if (language != "csharp")
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Output = string.Empty,
                    Error = $"Şu an sadece C# destekleniyor. Gönderilen dil: {input.Language}"
                };
            }

            if (string.IsNullOrWhiteSpace(input.Code))
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Output = string.Empty,
                    Error = "Çalıştırılacak kod boş olamaz."
                };
            }

            var blockedReason = ValidateCodeSafety(input.Code);
            if (!string.IsNullOrWhiteSpace(blockedReason))
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Output = string.Empty,
                    Error = blockedReason
                };
            }

            var timeoutMilliseconds = input.TimeoutMilliseconds <= 0
                ? 3000
                : input.TimeoutMilliseconds;

            await ConsoleLock.WaitAsync();

            var originalOut = Console.Out;
            var originalIn = Console.In;
            var outputBuilder = new StringBuilder();

            try
            {
                using var outputWriter = new StringWriter(outputBuilder);
                using var inputReader = new StringReader(input.InputText ?? string.Empty);

                Console.SetOut(outputWriter);
                Console.SetIn(inputReader);

                var options = ScriptOptions.Default
                    .AddReferences(
                        typeof(object).Assembly,
                        typeof(Enumerable).Assembly,
                        typeof(Console).Assembly
                    )
                    .AddImports(
                        "System",
                        "System.Linq",
                        "System.Collections.Generic"
                    );

                var script = CSharpScript.Create(input.Code, options);

                var compilation = script.GetCompilation();
                var diagnostics = compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                if (diagnostics.Any())
                {
                    return new CodeRunnerResult
                    {
                        ExitCode = 1,
                        Output = outputBuilder.ToString(),
                        Error = FormatDiagnostics(diagnostics)
                    };
                }

                using var cancellationTokenSource = new CancellationTokenSource(timeoutMilliseconds);

                try
                {
                    var state = await script.RunAsync(cancellationToken: cancellationTokenSource.Token);

                    if (state.Exception != null)
                    {
                        return new CodeRunnerResult
                        {
                            ExitCode = 1,
                            Output = outputBuilder.ToString(),
                            Error = FormatRuntimeException(state.Exception)
                        };
                    }

                    return new CodeRunnerResult
                    {
                        ExitCode = 0,
                        Output = outputBuilder.ToString(),
                        Error = null
                    };
                }
                catch (OperationCanceledException)
                {
                    return new CodeRunnerResult
                    {
                        ExitCode = 124,
                        Output = outputBuilder.ToString(),
                        Error = $"Kod çalışma süresi sınırını aştı. Maksimum süre: {timeoutMilliseconds} ms."
                    };
                }
            }
            catch (CompilationErrorException compilationException)
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Output = outputBuilder.ToString(),
                    Error = FormatDiagnostics(compilationException.Diagnostics)
                };
            }
            catch (Exception ex)
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Output = outputBuilder.ToString(),
                    Error = $"Beklenmeyen hata: {ex.GetType().Name} - {ex.Message}"
                };
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetIn(originalIn);
                ConsoleLock.Release();
            }
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

        private static string? ValidateCodeSafety(string code)
        {
            var forbiddenPatterns = new[]
            {
                "System.IO",
                "File.",
                "Directory.",
                "Path.",
                "Process",
                "System.Diagnostics",
                "Reflection",
                "Assembly.",
                "Activator.",
                "Environment.",
                "Thread.",
                "Task.Delay",
                "Task.Run",
                "while(true)",
                "while (true)",
                "for(;;)",
                "for (;;)"
            };

            foreach (var pattern in forbiddenPatterns)
            {
                if (code.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Güvenlik nedeniyle bu kod parçası çalıştırılamaz. Engellenen kullanım: {pattern}";
                }
            }

            return null;
        }

        private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
        {
            var errorBuilder = new StringBuilder();
            errorBuilder.AppendLine("Derleme hataları:");

            foreach (var diagnostic in diagnostics)
            {
                var span = diagnostic.Location.GetLineSpan();
                var line = span.StartLinePosition.Line + 1;
                var column = span.StartLinePosition.Character + 1;

                errorBuilder.AppendLine($"Satır {line}, Sütun {column}: {diagnostic.GetMessage()}");
            }

            return errorBuilder.ToString();
        }

        private static string FormatRuntimeException(Exception exception)
        {
            return $"Çalışma zamanı hatası: {exception.GetType().Name} - {exception.Message}";
        }
    }
}