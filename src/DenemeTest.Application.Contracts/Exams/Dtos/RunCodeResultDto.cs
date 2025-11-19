using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos;

public class RunCodeResultDto : EntityDto
{
    /// <summary>
    /// Çalıştırma başarılı mı? (Derleme/runtime hatası yoksa true)
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Programın STDOUT çıktısı (Console.WriteLine vs.)
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Hata mesajı (derleme veya runtime).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Process exit code (0 = başarılı).
    /// Şimdilik stub için 0 veya 1 gönderiyoruz.
    /// </summary>
    public int ExitCode { get; set; }
}
