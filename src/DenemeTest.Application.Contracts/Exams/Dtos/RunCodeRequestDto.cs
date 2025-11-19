using System;

namespace DenemeTest.Exams.Dtos
{
    public class RunCodeRequestDto
    {
        // Hangi oturumda (session) bu kod çalıştırılıyor
        public Guid SessionId { get; set; }

        // Hangi soruya ait kod
        public Guid QuestionId { get; set; }

        // Adayın yazdığı kod
        public string Code { get; set; } = string.Empty;

        // Şimdilik tek dil varsayalım; ileride C#, Java, Python vs. ekleyebiliriz
        public string Language { get; set; } = "csharp";

        // Test-case girişi (stdin / fonksiyon parametresi vs.)
        // Şu anki dummy executor bunu kullanmıyor ama gerçek implementasyonda buradan beslenecek.
        public string? Input { get; set; }
    }
}
