using System;
using Volo.Abp.Application.Dtos;

namespace DenemeTest.Exams.Dtos;

public class StartByTokenResultDto : EntityDto<Guid>
{
    public Guid TestId { get; set; }
    public Guid CandidateId { get; set; }
    public string CandidateName { get; set; }
}

//public class LeaderboardItemDto
//{
//    public Guid CandidateId { get; set; }
//    public string FirstName { get; set; }
//    public string LastName { get; set; }
//    public string Email { get; set; }
//    public int Score { get; set; }
//}
