using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class Score : FullAuditedAggregateRoot<Guid>
{
    public Guid ExamSessionId { get; protected set; }
    public int Value { get; protected set; } // 0-100
    public string? Explanation { get; protected set; } // yapay zekanın kısa açıklaması

    protected Score() { }

    public Score(Guid id, Guid examSessionId, int value, string? explanation)
        : base(id)
    {
        ExamSessionId = examSessionId;
        Value = value;
        Explanation = explanation;
    }

    public void Update(int value, string? explanation)
    {
        Value = value;
        Explanation = explanation;
    }
}
