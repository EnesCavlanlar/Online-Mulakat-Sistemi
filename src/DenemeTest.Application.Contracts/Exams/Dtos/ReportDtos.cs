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

    // 🔽 Proctoring / oturum durumu için ek alanlar
    public bool IsCancelled { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int ViolationCount { get; set; }
}
