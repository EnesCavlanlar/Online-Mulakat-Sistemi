using System;
using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos;

public class RunCodeRequestDto : EntityDto<Guid>
{
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }

    /// <summary>
    /// Kullanıcının yazdığı kod.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// "csharp", "python" vs. Şimdilik hep "csharp" göndereceğiz.
    /// </summary>
    public string Language { get; set; } = "csharp";

    /// <summary>
    /// Test-case input’u. Sınav sırasında otomatik puanlama için kullanılıyor.
    /// Kodu manuel çalıştırırken (Kodu Çalıştır butonu) genelde null olacak.
    /// </summary>
    public string? Input { get; set; }
}
