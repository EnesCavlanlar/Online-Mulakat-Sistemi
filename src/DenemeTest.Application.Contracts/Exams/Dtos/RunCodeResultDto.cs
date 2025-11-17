using System;

namespace DenemeTest.Exams.Dtos
{
    public class RunCodeResultDto
    {
        // Çalıştırma başarılı mı (derleme + testler)
        public bool Success { get; set; }

        // Standart çıktı (console output gibi düşünebilirsin)
        public string Output { get; set; } = string.Empty;

        // Hata mesajı (derleme hatası veya runtime hatası)
        public string Error { get; set; } = string.Empty;

        // İleride loglarda kullanmak için
        public DateTime ExecutedAt { get; set; }
    }
}
