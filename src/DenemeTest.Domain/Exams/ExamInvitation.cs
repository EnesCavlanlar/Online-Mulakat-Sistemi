using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class ExamInvitation : FullAuditedAggregateRoot<Guid>
{
    public Guid TestId { get; protected set; }
    public Guid CandidateId { get; protected set; }

    // Token artık düz metin tutulmaz. Sadece hash tutulur.
    public string TokenHash { get; protected set; } = default!;

    public DateTime ExpireAt { get; protected set; }

    public DateTime? SentAt { get; protected set; }
    public DateTime? UsedAt { get; protected set; }
    public bool IsUsed { get; protected set; }

    protected ExamInvitation()
    {
    }

    public ExamInvitation(
        Guid id,
        Guid testId,
        Guid candidateId,
        string tokenHash,
        DateTime expireAt
    ) : base(id)
    {
        TestId = testId;
        CandidateId = candidateId;
        TokenHash = tokenHash;
        ExpireAt = expireAt;
        IsUsed = false;
    }

    public void MarkSent()
    {
        SentAt = DateTime.UtcNow;
    }

    public void MarkUsed()
    {
        if (IsUsed)
        {
            return;
        }

        IsUsed = true;
        UsedAt = DateTime.UtcNow;
    }
}