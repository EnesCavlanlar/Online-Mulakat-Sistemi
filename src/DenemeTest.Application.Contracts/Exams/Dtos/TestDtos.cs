using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos;

public class TestDto : EntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool ShuffleQuestions { get; set; }

    public bool ShuffleOptions { get; set; }

    public int DurationMinutes { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public double PassScore { get; set; }
}

public class CreateUpdateTestDto
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool ShuffleQuestions { get; set; }

    public bool ShuffleOptions { get; set; }

    [Range(1, 1000)]
    public int DurationMinutes { get; set; } = 60;

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    [Range(0, 100)]
    public double PassScore { get; set; } = 50;
}