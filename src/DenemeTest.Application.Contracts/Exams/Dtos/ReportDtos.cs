using System;

namespace DenemeTest.Exams.Dtos;

public class LeaderboardItemDto
{
    public Guid CandidateId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int Score { get; set; }

    // İleride kayıt indirme için kullanılabilir:
    public Guid ExamSessionId { get; set; }
}
