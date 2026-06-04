using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos;

public class ExamInvitationDto : AuditedEntityDto<Guid>
{
    public Guid TestId { get; set; }
    public Guid CandidateId { get; set; }

    // Güvenlik için listelerde boş dönecek.
    // Sadece CreateAsync cevabında bir kere raw token verilebilir.
    public string? Token { get; set; }

    public DateTime ExpireAt { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public bool IsUsed { get; set; }
}

public class CreateExamInvitationDto
{
    [Required]
    public Guid TestId { get; set; }

    [Required]
    public Guid CandidateId { get; set; }

    [Required]
    public DateTime ExpireAt { get; set; }
}

public class BulkExamInvitationDto
{
    [Required]
    public Guid TestId { get; set; }

    [Required]
    public DateTime ExpireAt { get; set; }

    [Required]
    public List<Guid> CandidateIds { get; set; } = new();
}

public class BulkInvitationItemResultDto
{
    public Guid CandidateId { get; set; }

    public Guid? InvitationId { get; set; }

    public bool Success { get; set; }

    public string? Error { get; set; }
}

public class BulkInvitationResultDto
{
    public List<BulkInvitationItemResultDto> Items { get; set; } = new();

    public int SuccessCount { get; set; }

    public int FailureCount { get; set; }
}