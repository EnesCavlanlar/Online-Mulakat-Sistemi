using System;
using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos;

public class ExamInvitationDto : AuditedEntityDto<Guid>
{
    public Guid TestId { get; set; }
    public Guid CandidateId { get; set; }
    public string Token { get; set; }
    public DateTime ExpireAt { get; set; }
    public bool IsUsed { get; set; }
}

public class SendInvitationDto
{
    public Guid TestId { get; set; }
    public Guid CandidateId { get; set; }
    public DateTime ExpireAt { get; set; }
}
