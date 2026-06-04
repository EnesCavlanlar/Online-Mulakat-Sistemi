using DenemeTest.Application.Exams;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
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

            var normalizedCode = NormalizeCode(input.Code);

            var blockedReason = ValidateCodeSafety(normalizedCode);
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

            var compileResult = CompileToAssembly(normalizedCode);

            if (!compileResult.Success)
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Output = string.Empty,
                    Error = compileResult.Error
                };
            }

            await ConsoleLock.WaitAsync();

            var originalOut = Console.Out;
            var originalError = Console.Error;
            var originalIn = Console.In;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            AssemblyLoadContext? loadContext = null;

            try
            {
                using var outputWriter = new StringWriter(outputBuilder);
                using var errorWriter = new StringWriter(errorBuilder);
                using var inputReader = new StringReader(input.InputText ?? string.Empty);

                Console.SetOut(outputWriter);
                Console.SetError(errorWriter);
                Console.SetIn(inputReader);

                loadContext = new AssemblyLoadContext(
                    $"CandidateCodeContext_{Guid.NewGuid():N}",
                    isCollectible: true
                );

                var assemblyBytes = compileResult.AssemblyBytes!;
                using var assemblyStream = new MemoryStream(assemblyBytes);

                var assembly = loadContext.LoadFromStream(assemblyStream);

                var entryPoint = assembly.EntryPoint;
                if (entryPoint == null)
                {
                    return new CodeRunnerResult
                    {
                        ExitCode = 1,
                        Output = outputBuilder.ToString(),
                        Error = "Program içinde çalıştırılabilir bir Main metodu bulunamadı."
                    };
                }

                using var timeoutCts = new CancellationTokenSource();

                var executionTask = Task.Run(async () =>
                {
                    try
                    {
                        var parameters = entryPoint.GetParameters();

                        object?[]? args = null;

                        if (parameters.Length == 1 &&
                            parameters[0].ParameterType == typeof(string[]))
                        {
                            args = new object?[] { Array.Empty<string>() };
                        }
                        else if (parameters.Length == 0)
                        {
                            args = null;
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                "Main metodu parametresiz olmalı veya string[] args parametresi almalıdır."
                            );
                        }

                        var result = entryPoint.Invoke(null, args);

                        if (result is Task task)
                        {
                            await task;
                        }

                        return new ExecutionResult
                        {
                            ExitCode = 0,
                            Error = null
                        };
                    }
                    catch (TargetInvocationException ex)
                    {
                        var inner = ex.InnerException ?? ex;

                        return new ExecutionResult
                        {
                            ExitCode = 1,
                            Error = FormatRuntimeException(inner)
                        };
                    }
                    catch (Exception ex)
                    {
                        return new ExecutionResult
                        {
                            ExitCode = 1,
                            Error = FormatRuntimeException(ex)
                        };
                    }
                }, timeoutCts.Token);

                var completedTask = await Task.WhenAny(
                    executionTask,
                    Task.Delay(timeoutMilliseconds, timeoutCts.Token)
                );

                if (completedTask != executionTask)
                {
                    return new CodeRunnerResult
                    {
                        ExitCode = 124,
                        Output = outputBuilder.ToString(),
                        Error = $"Kod çalışma süresi sınırını aştı. Maksimum süre: {timeoutMilliseconds} ms."
                    };
                }

                var executionResult = await executionTask;

                var output = outputBuilder.ToString();
                var stdError = errorBuilder.ToString();

                var finalError = CombineErrors(executionResult.Error, stdError);

                return new CodeRunnerResult
                {
                    ExitCode = executionResult.ExitCode,
                    Output = output,
                    Error = finalError
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
                Console.SetError(originalError);
                Console.SetIn(originalIn);

                try
                {
                    loadContext?.Unload();
                }
                catch
                {
                    // unload başarısız olsa bile sınav akışını bozmayalım
                }

                ConsoleLock.Release();
            }
        }

        private static CompilationResult CompileToAssembly(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                code,
                new CSharpParseOptions(LanguageVersion.Preview)
            );

            var references = GetMetadataReferences();

            var compilation = CSharpCompilation.Create(
                assemblyName: $"CandidateProgram_{Guid.NewGuid():N}",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(
                    OutputKind.ConsoleApplication,
                    optimizationLevel: OptimizationLevel.Release,
                    allowUnsafe: false
                )
            );

            using var peStream = new MemoryStream();

            var emitResult = compilation.Emit(peStream);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .ToList();

                return new CompilationResult
                {
                    Success = false,
                    Error = FormatDiagnostics(errors)
                };
            }

            return new CompilationResult
            {
                Success = true,
                AssemblyBytes = peStream.ToArray()
            };
        }

        private static List<MetadataReference> GetMetadataReferences()
        {
            var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

            if (!string.IsNullOrWhiteSpace(trustedAssemblies))
            {
                return trustedAssemblies
                    .Split(Path.PathSeparator)
                    .Where(path =>
                        path.EndsWith("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("System.Console.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("System.Linq.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("System.Collections.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("System.Private.Uri.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("mscorlib.dll", StringComparison.OrdinalIgnoreCase)
                    )
                    .Select(path => MetadataReference.CreateFromFile(path))
                    .Cast<MetadataReference>()
                    .ToList();
            }

            return new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location)
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

        private static string NormalizeCode(string code)
        {
            var trimmedCode = code.Trim();

            if (ContainsMainMethod(trimmedCode))
            {
                return trimmedCode;
            }

            return $@"
using System;
using System.Linq;
using System.Collections.Generic;

public class Program
{{
    public static void Main()
    {{
{IndentCode(trimmedCode, 8)}
    }}
}}";
        }

        private static bool ContainsMainMethod(string code)
        {
            return code.Contains("static void Main", StringComparison.OrdinalIgnoreCase) ||
                   code.Contains("static int Main", StringComparison.OrdinalIgnoreCase) ||
                   code.Contains("static async Task Main", StringComparison.OrdinalIgnoreCase) ||
                   code.Contains("static Task Main", StringComparison.OrdinalIgnoreCase);
        }

        private static string IndentCode(string code, int spaces)
        {
            var indent = new string(' ', spaces);

            return string.Join(
                Environment.NewLine,
                code
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Split('\n')
                    .Select(line => indent + line)
            );
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
                "for (;;)",
                "unsafe",
                "DllImport",
                "Marshal.",
                "GC.",
                "Console.SetOut",
                "Console.SetIn",
                "Console.SetError",
                "AppDomain",
                "AssemblyLoadContext"
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

        private static string? CombineErrors(string? runtimeError, string? stdError)
        {
            var hasRuntimeError = !string.IsNullOrWhiteSpace(runtimeError);
            var hasStdError = !string.IsNullOrWhiteSpace(stdError);

            if (!hasRuntimeError && !hasStdError)
            {
                return null;
            }

            if (hasRuntimeError && !hasStdError)
            {
                return runtimeError;
            }

            if (!hasRuntimeError && hasStdError)
            {
                return stdError;
            }

            return runtimeError + Environment.NewLine + stdError;
        }

        private sealed class CompilationResult
        {
            public bool Success { get; set; }

            public byte[]? AssemblyBytes { get; set; }

            public string? Error { get; set; }
        }

        private sealed class ExecutionResult
        {
            public int ExitCode { get; set; }

            public string? Error { get; set; }
        }
    }
}