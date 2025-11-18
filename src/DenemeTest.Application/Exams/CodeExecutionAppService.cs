using System;
using System.Threading.Tasks;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Services;

namespace DenemeTest.Exams
{
    /// <summary>
    /// Şimdilik sadece dummy çalışan bir kod yürütme servisi.
    /// İleride buraya gerçek sandbox / Docker tabanlı execution gelecek.
    /// </summary>
    public class CodeExecutionAppService : ApplicationService, ICodeExecutionAppService
    {
        public async Task<RunCodeResultDto> RunAsync(RunCodeRequestDto input)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
            {
                return new RunCodeResultDto
                {
                    Success = false,
                    Output = string.Empty,
                    Error = "Kod alanı boş olamaz.",
                    ExecutedAt = Clock.Now
                };
            }

            // ----------------------------
            // DUMMY ERROR DETECTION
            // ----------------------------
            var hasDummyError = input.Code.Contains("error", StringComparison.OrdinalIgnoreCase);

            var result = new RunCodeResultDto
            {
                ExecutedAt = Clock.Now
            };

            if (hasDummyError)
            {
                result.Success = false;
                result.Output = string.Empty;
                result.Error = "Derleme hatası: 'error' kelimesi bulundu (dummy kontrol).";
                return result;
            }

            // ----------------------------
            // INPUT DESTEKLİ DUMMY OUTPUT
            // ----------------------------
            if (!string.IsNullOrWhiteSpace(input.Input))
            {
                // Test-case input’unu simüle ederek döndürüyoruz
                result.Success = true;
                result.Output = $"INPUT: {input.Input}".Trim();
                result.Error = string.Empty;
            }
            else
            {
                // Input yoksa varsayılan dummy çıktı
                result.Success = true;
                result.Output = "Kod başarıyla çalıştırıldı (dummy).";
                result.Error = string.Empty;
            }

            return await Task.FromResult(result);
        }
    }
}
