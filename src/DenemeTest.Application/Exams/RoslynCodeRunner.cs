using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DenemeTest.Application.Exams
{
    /// <summary>
    /// Roslyn tabanlı, in-process C# kod çalıştırıcı.
    /// 
    /// ÖNEMLİ:
    /// - Şu an için güvenlik açısından tam izole bir sandbox değildir.
    /// - Aynı process içinde kodu derleyip çalıştırır.
    /// - İleride Docker içinde çalışan gerçek sandbox ile değiştirilebilir.
    /// </summary>
    public class RoslynCodeRunner : ICodeRunner
    {
        public async Task<CodeRunnerResult> RunAsync(CodeRunnerInput input)
        {
            var result = new CodeRunnerResult();

            if (string.IsNullOrWhiteSpace(input.Code))
            {
                result.ExitCode = 1;
                result.Error = "Çalıştırılacak kod boş.";
                return result;
            }

            // Şimdilik sadece C# destekleniyor
            var lang = (input.Language ?? "csharp").ToLowerInvariant();
            if (lang is not ("csharp" or "cs"))
            {
                result.ExitCode = 1;
                result.Error = $"Şu an sadece C# destekleniyor. Gönderilen dil: {input.Language}";
                return result;
            }

            // Console IO yönlendirme
            var originalOut = Console.Out;
            var originalErr = Console.Error;
            var originalIn = Console.In;

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            using var inReader = new StringReader(input.InputText ?? string.Empty);

            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            Console.SetIn(inReader);

            string? errorMessage = null;

            try
            {
                var cts = new CancellationTokenSource(
                    input.TimeoutMilliseconds > 0 ? input.TimeoutMilliseconds : 3000);

                // Script seçenekleri: temel .NET referansları + sık kullanılan namespace'ler
                var scriptOptions = ScriptOptions.Default
                    .WithImports(
                        "System",
                        "System.Linq",
                        "System.Collections.Generic"
                    )
                    .WithReferences(
                        typeof(object).Assembly,
                        typeof(Enumerable).Assembly
                    );

                try
                {
                    // Kullanıcının kodunu script olarak çalıştırıyoruz
                    await CSharpScript.RunAsync(
                        input.Code,
                        scriptOptions,
                        cancellationToken: cts.Token
                    );

                    result.ExitCode = 0;
                }
                catch (OperationCanceledException)
                {
                    result.ExitCode = -1;
                    errorMessage = "Kod çalıştırma zaman aşımına uğradı.";
                }
                catch (CompilationErrorException ex)
                {
                    result.ExitCode = 1;
                    errorMessage = "Derleme hatası:\n" + string.Join(Environment.NewLine, ex.Diagnostics);
                }
                catch (Exception ex)
                {
                    result.ExitCode = 1;
                    errorMessage = "Çalışma zamanı hatası:\n" + ex;
                }

                // STDOUT / STDERR toplanıyor
                var stdOut = outWriter.ToString();
                var stdErr = errWriter.ToString();

                result.Output = string.IsNullOrWhiteSpace(stdOut) ? null : stdOut;
                result.Error = BuildErrorText(errorMessage, stdErr);
            }
            finally
            {
                // Console IO'yu eski haline getir
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
                Console.SetIn(originalIn);
            }

            return result;
        }

        private static string? BuildErrorText(string? mainError, string? stderr)
        {
            var hasMain = !string.IsNullOrWhiteSpace(mainError);
            var hasStdErr = !string.IsNullOrWhiteSpace(stderr);

            if (!hasMain && !hasStdErr)
                return null;

            if (hasMain && !hasStdErr)
                return mainError;

            if (!hasMain && hasStdErr)
                return stderr;

            // İkisi de varsa alt alta birleştir
            return mainError + Environment.NewLine + stderr;
        }
    }
}
