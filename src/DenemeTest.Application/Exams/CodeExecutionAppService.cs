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

            // Şimdilik tamamen yapay bir kontrol:
            // Kod string'i içinde "error" kelimesi geçiyorsa derleme hatası varsayıyoruz.
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
            }
            else
            {
                result.Success = true;
                result.Output = "Kod başarıyla çalıştırıldı (dummy). Buraya gerçek çıktı gelecek.";
                result.Error = string.Empty;
            }

            // Şimdilik async yapıya uymak için Task.FromResult
            return await Task.FromResult(result);
        }
    }
}
