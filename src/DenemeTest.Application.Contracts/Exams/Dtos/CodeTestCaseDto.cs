using System;

namespace DenemeTest.Exams.Dtos;

public class CodeTestCaseDto
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public int Weight { get; set; } = 1;
}
