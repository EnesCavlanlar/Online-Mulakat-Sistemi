using System.Collections.Generic;

namespace DenemeTest.Exams.Dtos
{
    public class RunCodeResultDto
    {
        public bool Success { get; set; }

        public int ExitCode { get; set; }

        /// <summary>
        /// Tüm test case sonuçları.
        /// </summary>
        public List<TestCaseResultDto> TestCases { get; set; } = new();

        /// <summary>
        /// Geçen (doğru çalışan) test-case sayısı.
        /// </summary>
        public int PassedCount { get; set; }

        /// <summary>
        /// Toplam test-case sayısı.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Eski UI ile uyumluluk için özet çıktı metni.
        /// Örn: "3/4 test geçti" veya her test için kısa satırlar.
        /// </summary>
        public string? Output { get; set; }

        public string? Error { get; set; }
    }
}
