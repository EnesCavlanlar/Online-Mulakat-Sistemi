using System;
using System.Collections.Generic;

namespace DenemeTest.Exams.Dtos;

// ---------------- Question / Option ----------------

public class QuestionOptionRunDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = default!;
}

public class QuestionRunDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = default!;

    // Contracts katmanında DTO enum kullanıyoruz
    public QuestionTypeDto Type { get; set; }

    public int Points { get; set; }
    public List<QuestionOptionRunDto> Options { get; set; } = new();
}

// ---------------- Test (Run) ----------------

public class TestRunDto
{
    public Guid TestId { get; set; }
    public string TestName { get; set; } = default!;
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }

    public int DurationMinutes { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    public List<QuestionRunDto> Questions { get; set; } = new();
}

// ---------------- Start with token result ----------------

public class StartWithTokenResultDto
{
    public Guid SessionId { get; set; }
    public string? TestName { get; set; }
}
