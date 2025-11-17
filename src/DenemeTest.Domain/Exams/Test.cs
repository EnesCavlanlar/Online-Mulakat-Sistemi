using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class Test : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; protected set; }
    public string? Description { get; protected set; }
    public DateTime? StartAt { get; protected set; }
    public DateTime? EndAt { get; protected set; }
    public bool ShuffleQuestions { get; protected set; }
    public bool ShuffleOptions { get; protected set; }

    // ✅ Yeni alanlar
    public int DurationMinutes { get; protected set; }      // Sınav süresi (dakika)
    public double PassScore { get; protected set; }         // Geçme puanı

    public virtual ICollection<Question> Questions { get; protected set; }

    protected Test() { }

    public Test(
        Guid id,
        string name,
        string? description = null,
        DateTime? startAt = null,
        DateTime? endAt = null,
        bool shuffleQuestions = true,
        bool shuffleOptions = true,
        int durationMinutes = 60,
        double passScore = 50
    ) : base(id)
    {
        Name = name;
        Description = description;
        StartAt = startAt;
        EndAt = endAt;
        ShuffleQuestions = shuffleQuestions;
        ShuffleOptions = shuffleOptions;
        DurationMinutes = durationMinutes;
        PassScore = passScore;
        Questions = new List<Question>();
    }

    public void Update(
        string name,
        string? description,
        DateTime? startAt,
        DateTime? endAt,
        bool shuffleQuestions,
        bool shuffleOptions,
        int durationMinutes,
        double passScore
    )
    {
        Name = name;
        Description = description;
        StartAt = startAt;
        EndAt = endAt;
        ShuffleQuestions = shuffleQuestions;
        ShuffleOptions = shuffleOptions;
        DurationMinutes = durationMinutes;
        PassScore = passScore;
    }
}
