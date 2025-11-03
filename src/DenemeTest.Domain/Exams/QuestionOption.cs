using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class QuestionOption : FullAuditedAggregateRoot<Guid>
{
    public Guid QuestionId { get; protected set; }
    public string Text { get; protected set; }
    public bool IsCorrect { get; protected set; }

    protected QuestionOption() { }

    public QuestionOption(Guid id, Guid questionId, string text, bool isCorrect)
        : base(id)
    {
        QuestionId = questionId;
        Text = text;
        IsCorrect = isCorrect;
    }

    public void Update(string text, bool isCorrect)
    {
        Text = text;
        IsCorrect = isCorrect;
    }
}
