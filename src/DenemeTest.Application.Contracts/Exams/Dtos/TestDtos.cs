using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos;

public class TestDto : EntityDto<Guid>
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
}

public class CreateUpdateTestDto
{
    [Required, StringLength(256)]
    public string Name { get; set; }
    [StringLength(2000)]
    public string? Description { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
}
