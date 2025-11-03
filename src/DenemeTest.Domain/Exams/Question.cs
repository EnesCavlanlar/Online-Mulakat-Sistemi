using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class Question : FullAuditedAggregateRoot<Guid>
{
    public Guid TestId { get; protected set; }
    public QuestionType Type { get; protected set; }
    public string Text { get; protected set; }
    public int Points { get; protected set; }

    public virtual ICollection<QuestionOption> Options { get; protected set; }

    protected Question() { }

    public Question(Guid id, Guid testId, QuestionType type, string text, int points)
        : base(id)
    {
        TestId = testId;
        Type = type;
        Text = text;
        Points = points;
        Options = new List<QuestionOption>();
    }

    public void Update(QuestionType type, string text, int points)
    {
        Type = type;
        Text = text;
        Points = points;
    }
}
