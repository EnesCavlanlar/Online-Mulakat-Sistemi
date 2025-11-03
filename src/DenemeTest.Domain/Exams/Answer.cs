using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class Answer : FullAuditedAggregateRoot<Guid>
{
    public Guid ExamSessionId { get; protected set; }
    public Guid QuestionId { get; protected set; }
    public string? TextAnswer { get; protected set; } // klasik için
    public Guid[]? SelectedOptionIds { get; protected set; } // çoktan seçmeli için

    protected Answer() { }

    public Answer(Guid id, Guid examSessionId, Guid questionId, string? textAnswer, Guid[]? selectedOptionIds)
        : base(id)
    {
        ExamSessionId = examSessionId;
        QuestionId = questionId;
        TextAnswer = textAnswer;
        SelectedOptionIds = selectedOptionIds;
    }

    public void UpdateText(string? text) => TextAnswer = text;
    public void UpdateOptions(Guid[]? optionIds) => SelectedOptionIds = optionIds;
}
