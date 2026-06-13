using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams
{
    public class CodeReview : FullAuditedAggregateRoot<Guid>
    {
        public Guid ExamSessionId { get; private set; }

        public Guid QuestionId { get; private set; }

        public bool TestsPassed { get; private set; }

        public int PassedCount { get; private set; }

        public int TotalCount { get; private set; }

        public bool IsSuspicious { get; private set; }

        public int? QualityScore { get; private set; }

        public string Summary { get; private set; } = string.Empty;

        public string Flags { get; private set; } = string.Empty;

        public string Provider { get; private set; } = string.Empty;

        protected CodeReview()
        {
        }

        public CodeReview(
            Guid id,
            Guid examSessionId,
            Guid questionId,
            bool testsPassed,
            int passedCount,
            int totalCount,
            bool isSuspicious,
            int? qualityScore,
            string? summary,
            string? flags,
            string? provider)
            : base(id)
        {
            ExamSessionId = examSessionId;
            QuestionId = questionId;
            TestsPassed = testsPassed;
            PassedCount = passedCount;
            TotalCount = totalCount;
            IsSuspicious = isSuspicious;
            QualityScore = qualityScore;
            Summary = summary ?? string.Empty;
            Flags = flags ?? string.Empty;
            Provider = provider ?? string.Empty;
        }

        public void Update(
            bool testsPassed,
            int passedCount,
            int totalCount,
            bool isSuspicious,
            int? qualityScore,
            string? summary,
            string? flags,
            string? provider)
        {
            TestsPassed = testsPassed;
            PassedCount = passedCount;
            TotalCount = totalCount;
            IsSuspicious = isSuspicious;
            QualityScore = qualityScore;
            Summary = summary ?? string.Empty;
            Flags = flags ?? string.Empty;
            Provider = provider ?? string.Empty;
        }
    }
}