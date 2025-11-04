using System;
using System.Collections.Generic;

namespace DenemeTest.Exams.Dtos;

public class QuestionOptionRunDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = default!;
}

public class QuestionRunDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = default!;
    public QuestionTypeDto Type { get; set; }                 // << DTO ENUM
    public int Points { get; set; }
    public List<QuestionOptionRunDto> Options { get; set; } = new();
}

public class TestRunDto
{
    public Guid TestId { get; set; }
    public string TestName { get; set; } = default!;
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
    public List<QuestionRunDto> Questions { get; set; } = new();
}
