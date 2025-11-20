using System;

namespace DenemeTest.Exams.Dtos
{
    /// <summary>
    /// Kod çalıştırma isteği DTO'su.
    /// </summary>
    public class RunCodeRequestDto
    {
        /// <summary>
        /// Oturum (session) id'si – loglama vs. için.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>
        /// İlgili soru Id'si.
        /// </summary>
        public Guid QuestionId { get; set; }

        /// <summary>
        /// Kullanıcının yazdığı kod.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Programlama dili (şimdilik "csharp").
        /// </summary>
        public string Language { get; set; } = "csharp";

        /// <summary>
        /// Bu çalıştırma için kullanılacak input (STDIN).
        /// Coding soru puanlamasında her test-case için ayrı input gönderiliyor.
        /// </summary>
        public string? Input { get; set; }
    }
}
