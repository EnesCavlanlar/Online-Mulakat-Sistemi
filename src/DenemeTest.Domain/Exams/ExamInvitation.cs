using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class ExamInvitation : FullAuditedAggregateRoot<Guid>
{
    public Guid TestId { get; protected set; }
    public Guid CandidateId { get; protected set; }

    public string Token { get; protected set; } = default!; // Tek kullanımlık bağlantı
    public DateTime ExpireAt { get; protected set; }

    public DateTime? SentAt { get; protected set; }  // E-posta gönderildiği zaman
    public DateTime? UsedAt { get; protected set; }  // Token kullanıldığı zaman
    public bool IsUsed { get; protected set; }       // Kullanıldı mı?

    protected ExamInvitation() { }

    public ExamInvitation(
        Guid id,
        Guid testId,
        Guid candidateId,
        string token,
        DateTime expireAt
    ) : base(id)
    {
        TestId = testId;
        CandidateId = candidateId;
        Token = token;
        ExpireAt = expireAt;
        IsUsed = false;
    }

    // 🔹 E-posta gönderildiğinde çağrılır
    public void MarkSent()
    {
        SentAt = DateTime.UtcNow;
    }

    // 🔹 Davet linki kullanıldığında çağrılır
    public void MarkUsed()
    {
        IsUsed = true;
        UsedAt = DateTime.UtcNow;
    }
}
