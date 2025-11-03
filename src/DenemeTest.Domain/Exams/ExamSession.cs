using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class ExamSession : FullAuditedAggregateRoot<Guid>
{
    public Guid TestId { get; protected set; }
    public Guid CandidateId { get; protected set; }
    public DateTime StartedAt { get; protected set; }
    public DateTime? FinishedAt { get; protected set; }
    public bool IsCancelled { get; protected set; }
    public string? CancelReason { get; protected set; }

    protected ExamSession() { }

    public ExamSession(Guid id, Guid testId, Guid candidateId, DateTime startedAt)
        : base(id)
    {
        TestId = testId;
        CandidateId = candidateId;
        StartedAt = startedAt;
    }

    public void Finish(DateTime finishedAt) => FinishedAt = finishedAt;

    public void Cancel(string reason)
    {
        IsCancelled = true;
        CancelReason = reason;
    }
}
