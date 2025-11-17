using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

/// <summary>
/// Kod soruları için giriş/çıkış ve ağırlık tanımı.
/// </summary>
public class CodeTestCase : AuditedAggregateRoot<Guid>
{
    public Guid QuestionId { get; set; }

    /// <summary>Program stdin olarak alacak.</summary>
    public string Input { get; set; } = "";

    /// <summary>stdout ile birebir karşılaştırılır (Trim).</summary>
    public string ExpectedOutput { get; set; } = "";

    /// <summary>Her testcase'in puana katkısı (varsayılan 1).</summary>
    public int Weight { get; set; } = 1;
}
