using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DenemeTest.Exams.Dtos;

public class QuestionOptionDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = default!;
    public bool IsCorrect { get; set; }
}

public class QuestionDto
{
    public Guid Id { get; set; }
    public Guid TestId { get; set; }
    public QuestionTypeDto Type { get; set; }                // << DTO ENUM
    public string Text { get; set; } = default!;
    public int Points { get; set; }
    public List<QuestionOptionDto> Options { get; set; } = new();
}

public class CreateUpdateQuestionOptionDto
{
    [Required, StringLength(2000)]
    public string Text { get; set; } = default!;
    public bool IsCorrect { get; set; }
}

public class CreateUpdateQuestionDto
{
    [Required] public Guid TestId { get; set; }
    [Required] public QuestionTypeDto Type { get; set; }     // << DTO ENUM
    [Required, StringLength(4000)] public string Text { get; set; } = default!;
    public int Points { get; set; } = 1;
    public List<CreateUpdateQuestionOptionDto> Options { get; set; } = new();
}
