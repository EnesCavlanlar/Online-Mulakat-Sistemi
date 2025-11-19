using System;
using System.Collections.Generic;
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

// 🔥 TOPLU DAVET DTO'LARI

// Admin: "Şu test için şu adaylara davet oluştur" diyecek
public class BulkExamInvitationDto
{
    [Required]
    public Guid TestId { get; set; }

    [Required]
    public DateTime ExpireAt { get; set; }

    // 100 aday = 100 CandidateId
    [Required]
    public List<Guid> CandidateIds { get; set; } = new();
}

// Her aday için sonuç
public class BulkInvitationItemResultDto
{
    public Guid CandidateId { get; set; }

    public Guid? InvitationId { get; set; }

    // Success = true → InvitationId dolu
    // Success = false → Error dolu
    public bool Success { get; set; }

    public string? Error { get; set; }
}

// Toplu sonuç özeti
public class BulkInvitationResultDto
{
    public List<BulkInvitationItemResultDto> Items { get; set; } = new();

    public int SuccessCount { get; set; }

    public int FailureCount { get; set; }
}
