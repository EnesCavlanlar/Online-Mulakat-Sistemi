using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Services;

namespace DenemeTest.Application.Exams;

/// <summary>
/// Şimdilik DEMO/STUB code executor.
/// 
/// - Gerçek anlamda C# kodu çalıştırmıyor.
/// - Güvenlik için kodu derleyip çalıştırmıyoruz.
/// - UI ve puanlama akışını test etmek için kullanıyoruz.
/// 
/// İleride burayı Docker içinde gerçek sandbox ile değiştireceğiz.
/// </summary>
public class CodeExecutionAppService : ApplicationService, ICodeExecutionAppService
{
    public async Task<RunCodeResultDto> RunAsync(RunCodeRequestDto input)
    {
        // Asenkron signature'ı korumak için
        await Task.Yield();

        // Çok basit bir davranış:
        // - Eğer kod boşsa hata dön
        // - Değilse "demo çıktısı" üret
        if (string.IsNullOrWhiteSpace(input.Code))
        {
            return new RunCodeResultDto
            {
                Success = false,
                ExitCode = 1,
                Output = string.Empty,
                Error = "Kod boş gönderildi."
            };
        }

        // Gerçek sistemde burada:
        // 1) Kod dosyasını oluştur
        // 2) Docker konteyner içinde derle/çalıştır
        // 3) STDOUT/STDERR'i al ve dön
        // Şimdilik sadece demo mesajı dönüyoruz.
        var output = $"[Demo executor] Dil: {input.Language} | Kod uzunluğu: {input.Code.Length} karakter.";

        return new RunCodeResultDto
        {
            Success = true,
            ExitCode = 0,
            Output = output,
            Error = null
        };
    }
}
