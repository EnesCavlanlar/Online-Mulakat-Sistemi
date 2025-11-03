using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class ProctoringEvent : FullAuditedAggregateRoot<Guid>
{
    public Guid ExamSessionId { get; protected set; }
    public ProctoringEventType Type { get; protected set; }
    public string? Detail { get; protected set; }

    protected ProctoringEvent() { }

    public ProctoringEvent(Guid id, Guid examSessionId, ProctoringEventType type, string? detail)
        : base(id)
    {
        ExamSessionId = examSessionId;
        Type = type;
        Detail = detail;
    }
}
