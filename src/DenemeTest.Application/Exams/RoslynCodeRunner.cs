using DenemeTest.Application.Exams;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DenemeTest.Exams
{
    public class RoslynCodeRunner : ICodeRunner
    {
        public async Task<CodeRunnerResult> RunAsync(CodeRunnerInput input)
        {
            // Şimdilik sadece C# destekliyoruz
            var lang = (input.Language ?? "csharp").ToLowerInvariant();
            if (lang is not ("csharp" or "cs"))
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Error = $"Şu an sadece C# destekleniyor. Gönderilen dil: {input.Language}"
                };
            }

            var options = ScriptOptions.Default
                .AddReferences(
                    typeof(object).Assembly,
                    typeof(Enumerable).Assembly
                )
                .AddImports(
                    "System",
                    "System.Linq",
                    "System.Collections.Generic"
                );

            var sbOutput = new StringBuilder();
            var originalOut = Console.Out;

            try
            {
                using var writer = new StringWriter(sbOutput);
                Console.SetOut(writer);

                // Önce compile edip hata var mı bakıyoruz
                var script = CSharpScript.Create(input.Code ?? string.Empty, options);
                var compilation = script.GetCompilation();
                var diagnostics = compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                if (diagnostics.Any())
                {
                    var err = new StringBuilder();
                    err.AppendLine("Derleme hataları:");

                    foreach (var d in diagnostics)
                    {
                        var span = d.Location.GetLineSpan();
                        var line = span.StartLinePosition.Line + 1;
                        var col = span.StartLinePosition.Character + 1;
                        err.AppendLine($"Satır {line}, Sütun {col}: {d.GetMessage()}");
                    }

                    return new CodeRunnerResult
                    {
                        ExitCode = 1,
                        Error = err.ToString()
                    };
                }

                // Compile temiz → script'i çalıştır
                var state = await script.RunAsync();

                // Runtime exception varsa yakala
                if (state.Exception != null)
                {
                    var ex = state.Exception;
                    var msg = $"Çalışma zamanı hatası: {ex.GetType().Name} - {ex.Message}";
                    return new CodeRunnerResult
                    {
                        ExitCode = 1,
                        Output = sbOutput.ToString(),
                        Error = msg
                    };
                }

                return new CodeRunnerResult
                {
                    ExitCode = 0,
                    Output = sbOutput.ToString(),
                    Error = null
                };
            }
            catch (CompilationErrorException cex)
            {
                var err = new StringBuilder();
                err.AppendLine("Derleme hataları:");
                foreach (var d in cex.Diagnostics)
                {
                    var span = d.Location.GetLineSpan();
                    var line = span.StartLinePosition.Line + 1;
                    var col = span.StartLinePosition.Character + 1;
                    err.AppendLine($"Satır {line}, Sütun {col}: {d.GetMessage()}");
                }

                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Error = err.ToString()
                };
            }
            catch (Exception ex)
            {
                return new CodeRunnerResult
                {
                    ExitCode = 1,
                    Error = $"Beklenmeyen hata: {ex.GetType().Name} - {ex.Message}"
                };
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
