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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DenemeTest.Exams
{
    public class RoslynCodeRunner : ICodeRunner
    {
        private static readonly SemaphoreSlim ConsoleLock = new(1, 1);

        private const int MaxCodeCharacters = 20_000;
        private const int MaxInputCharacters = 20_000;
        private const int MaxOutputCharacters = 100_000;
        private const int DefaultTimeoutMilliseconds = 3000;
        private const int MaxTimeoutMilliseconds = 5000;

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

            if (input.Code.Length > MaxCodeCharacters)
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Output = string.Empty,
                    Error = $"Kod çok uzun. Maksimum izin verilen karakter sayısı: {MaxCodeCharacters}."
                };
            }

            if (!string.IsNullOrEmpty(input.InputText) && input.InputText.Length > MaxInputCharacters)
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Output = string.Empty,
                    Error = $"Input çok uzun. Maksimum izin verilen karakter sayısı: {MaxInputCharacters}."
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

            var timeoutMilliseconds = NormalizeTimeout(input.TimeoutMilliseconds);

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

            LimitedStringWriter? outputWriter = null;
            LimitedStringWriter? errorWriter = null;
            StringReader? inputReader = null;

            try
            {
                outputWriter = new LimitedStringWriter(outputBuilder, MaxOutputCharacters);
                errorWriter = new LimitedStringWriter(errorBuilder, MaxOutputCharacters);
                inputReader = new StringReader(input.InputText ?? string.Empty);

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
                        Output = BuildFinalOutput(outputBuilder, outputWriter),
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
                        Output = BuildFinalOutput(outputBuilder, outputWriter),
                        Error = $"Kod çalışma süresi sınırını aştı. Maksimum süre: {timeoutMilliseconds} ms."
                    };
                }

                timeoutCts.Cancel();

                var executionResult = await executionTask;

                var output = BuildFinalOutput(outputBuilder, outputWriter);
                var stdError = BuildFinalOutput(errorBuilder, errorWriter);

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
                    Output = BuildFinalOutput(outputBuilder, outputWriter),
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
                    outputWriter?.Dispose();
                    errorWriter?.Dispose();
                    inputReader?.Dispose();
                }
                catch
                {
                }

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
                    allowUnsafe: false,
                    checkOverflow: false
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
                var allowedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "System.Private.CoreLib.dll",
                    "System.Runtime.dll",
                    "System.Console.dll",
                    "System.Linq.dll",
                    "System.Collections.dll",
                    "System.Collections.NonGeneric.dll",
                    "System.Text.RegularExpressions.dll",
                    "System.Text.Encoding.Extensions.dll",
                    "netstandard.dll",
                    "mscorlib.dll"
                };

                return trustedAssemblies
                    .Split(Path.PathSeparator)
                    .Where(path => allowedAssemblyNames.Contains(Path.GetFileName(path)))
                    .Select(path => MetadataReference.CreateFromFile(path))
                    .Cast<MetadataReference>()
                    .ToList();
            }

            return new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Regex).Assembly.Location)
            };
        }

        private static int NormalizeTimeout(int requestedTimeoutMilliseconds)
        {
            if (requestedTimeoutMilliseconds <= 0)
            {
                return DefaultTimeoutMilliseconds;
            }

            return Math.Clamp(
                requestedTimeoutMilliseconds,
                500,
                MaxTimeoutMilliseconds
            );
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
using System.Text.RegularExpressions;

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
            var compactCode = Regex.Replace(code, @"\s+", string.Empty).ToLowerInvariant();
            var normalizedCode = code.ToLowerInvariant();

            var forbiddenContains = new[]
            {
                "system.io",
                "file.",
                "directory.",
                "path.",
                "driveinfo",
                "filestream",
                "streamreader",
                "streamwriter",

                "system.net",
                "httpclient",
                "webclient",
                "socket",
                "tcpclient",
                "udpclient",
                "dns.",

                "system.diagnostics",
                "process",
                "processstartinfo",

                "reflection",
                "assembly.",
                "methodinfo",
                "propertyinfo",
                "fieldinfo",
                "activator.",
                "gettype(",

                "environment.",
                "appcontext.",
                "appdomain",
                "assemblyloadcontext",

                "thread.",
                "threadsleep",
                "system.threading",
                "task.delay",
                "task.run",
                "parallel.",
                "timer",

                "unsafe",
                "dllimport",
                "marshal.",
                "intptr",
                "uintptr",
                "stackalloc",
                "fixed(",

                "console.setout",
                "console.setin",
                "console.seterror",
                "console.openstandardinput",
                "console.openstandardoutput",
                "console.openstandarderror",
                "console.readkey",

                "gc.",
                "goto ",
                "dynamic ",
                "#r ",
                "#load "
            };

            foreach (var pattern in forbiddenContains)
            {
                if (normalizedCode.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    compactCode.Contains(pattern.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase))
                {
                    return $"Güvenlik nedeniyle bu kod parçası çalıştırılamaz. Engellenen kullanım: {pattern}";
                }
            }

            var forbiddenRegexPatterns = new Dictionary<string, string>
            {
                { @"while\s*\(\s*true\s*\)", "while(true)" },
                { @"for\s*\(\s*;\s*;\s*\)", "for(;;)" },
                { @"do\s*\{", "do-while döngüsü" },
                { @"new\s+thread\s*\(", "new Thread(...)" },
                { @"thread\s*\.\s*sleep\s*\(", "Thread.Sleep(...)" },
                { @"task\s*\.\s*delay\s*\(", "Task.Delay(...)" },
                { @"task\s*\.\s*run\s*\(", "Task.Run(...)" },
                { @"process\s*\.\s*start\s*\(", "Process.Start(...)" },
                { @"activator\s*\.\s*createinstance\s*\(", "Activator.CreateInstance(...)" },
                { @"assembly\s*\.\s*load", "Assembly.Load(...)" },
                { @"type\s*\.\s*gettype\s*\(", "Type.GetType(...)" },
                { @"console\s*\.\s*setout\s*\(", "Console.SetOut(...)" },
                { @"console\s*\.\s*setin\s*\(", "Console.SetIn(...)" },
                { @"console\s*\.\s*seterror\s*\(", "Console.SetError(...)" }
            };

            foreach (var item in forbiddenRegexPatterns)
            {
                if (Regex.IsMatch(code, item.Key, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    return $"Güvenlik nedeniyle bu kod parçası çalıştırılamaz. Engellenen kullanım: {item.Value}";
                }
            }

            return null;
        }

        private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
        {
            var errorBuilder = new StringBuilder();
            errorBuilder.AppendLine("Derleme hataları:");

            foreach (var diagnostic in diagnostics.Take(20))
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

        private static string BuildFinalOutput(
            StringBuilder builder,
            LimitedStringWriter? writer)
        {
            var output = builder.ToString();

            if (writer?.Truncated == true)
            {
                output += Environment.NewLine +
                          "[Çıktı çok uzun olduğu için sistem tarafından kesildi.]";
            }

            return output;
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

        private sealed class LimitedStringWriter : StringWriter
        {
            private readonly int _maxCharacters;
            private int _writtenCharacters;

            public bool Truncated { get; private set; }

            public LimitedStringWriter(StringBuilder builder, int maxCharacters)
                : base(builder)
            {
                _maxCharacters = Math.Max(1, maxCharacters);
            }

            public override void Write(char value)
            {
                if (_writtenCharacters >= _maxCharacters)
                {
                    Truncated = true;
                    return;
                }

                base.Write(value);
                _writtenCharacters++;
            }

            public override void Write(string? value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }

                var remaining = _maxCharacters - _writtenCharacters;

                if (remaining <= 0)
                {
                    Truncated = true;
                    return;
                }

                if (value.Length > remaining)
                {
                    base.Write(value.Substring(0, remaining));
                    _writtenCharacters += remaining;
                    Truncated = true;
                    return;
                }

                base.Write(value);
                _writtenCharacters += value.Length;
            }

            public override void Write(char[] buffer, int index, int count)
            {
                if (buffer == null || count <= 0)
                {
                    return;
                }

                var remaining = _maxCharacters - _writtenCharacters;

                if (remaining <= 0)
                {
                    Truncated = true;
                    return;
                }

                var safeCount = Math.Min(count, remaining);

                base.Write(buffer, index, safeCount);
                _writtenCharacters += safeCount;

                if (safeCount < count)
                {
                    Truncated = true;
                }
            }

            public override void WriteLine(string? value)
            {
                Write(value);
                Write(Environment.NewLine);
            }
        }
    }
}