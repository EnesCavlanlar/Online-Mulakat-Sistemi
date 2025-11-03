using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class ExamInvitation : FullAuditedAggregateRoot<Guid>
{
    public Guid TestId { get; protected set; }
    public Guid CandidateId { get; protected set; }
    public string Token { get; protected set; } // tek kullanımlık
    public DateTime ExpireAt { get; protected set; }
    public bool IsUsed { get; protected set; }

    protected ExamInvitation() { }

    public ExamInvitation(Guid id, Guid testId, Guid candidateId, string token, DateTime expireAt)
        : base(id)
    {
        TestId = testId;
        CandidateId = candidateId;
        Token = token;
        ExpireAt = expireAt;
        IsUsed = false;
    }

    public void MarkUsed() => IsUsed = true;
}
