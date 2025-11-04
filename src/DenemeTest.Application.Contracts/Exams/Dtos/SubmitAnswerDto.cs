using System;

namespace DenemeTest.Exams.Dtos;

public class SubmitAnswerDto
{
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? TextAnswer { get; set; }
    public Guid[]? SelectedOptionIds { get; set; }
    public Guid SessionId { get; set; }
}
