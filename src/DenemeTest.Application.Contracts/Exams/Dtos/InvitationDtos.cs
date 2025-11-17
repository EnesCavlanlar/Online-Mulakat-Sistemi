using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos;

public class ExamInvitationDto : AuditedEntityDto<Guid>
{
    public Guid TestId { get; set; }
    public Guid CandidateId { get; set; }
    public string Token { get; set; } = default!;
    public DateTime ExpireAt { get; set; }

    public DateTime? SentAt { get; set; }   // E-posta gönderim zamanı
    public DateTime? UsedAt { get; set; }   // Token kullanıldığı zaman
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
