using System.Threading.Tasks;

namespace DenemeTest.Application.Exams
{
    /// <summary>
    /// Uygulama içinde kod çalıştırma motoru için arayüz.
    /// 
    /// Şu an için:
    /// - Sadece C# dilini destekleyen bir Roslyn tabanlı implementasyon (RoslynCodeRunner) kullanılacak.
    /// - İleride Docker sandbox, başka diller vb. için farklı implementasyonlar yazılabilir.
    /// </summary>
    public interface ICodeRunner
    {
        Task<CodeRunnerResult> RunAsync(CodeRunnerInput input);
    }

    /// <summary>
    /// Kod çalıştırma isteği için input modeli.
    /// </summary>
    public class CodeRunnerInput
    {
        /// <summary>
        /// Çalıştırılacak kaynak kod (zorunlu).
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Dil bilgisi (örn: "csharp", "cs").
        /// Şimdilik sadece C# destekleniyor.
        /// </summary>
        public string Language { get; set; } = "csharp";

        /// <summary>
        /// Programın stdin üzerinden okuyacağı giriş (Console.ReadLine vb.).
        /// </summary>
        public string? InputText { get; set; }

        /// <summary>
        /// Maksimum çalışma süresi (ms).
        /// Süre aşılırsa zaman aşımı hatası dönülür.
        /// </summary>
        public int TimeoutMilliseconds { get; set; } = 3000;
    }

    /// <summary>
    /// Kod çalıştırma sonucunda dönen output modeli.
    /// </summary>
    public class CodeRunnerResult
    {
        /// <summary>
        /// 0 ise başarı, 0 dışı değerler hata olarak yorumlanabilir.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// STDOUT çıktısı (Console.Write/WriteLine).
        /// </summary>
        public string? Output { get; set; }

        /// <summary>
        /// STDERR ya da yakalanan exception bilgisi.
        /// </summary>
        public string? Error { get; set; }
    }
}
