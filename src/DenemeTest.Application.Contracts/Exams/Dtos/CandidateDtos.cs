using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos;

public class CandidateDto : EntityDto<Guid>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

public class CreateUpdateCandidateDto
{
    [Required, StringLength(128)]
    public string FirstName { get; set; }

    [Required, StringLength(128)]
    public string LastName { get; set; }

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; }
}
