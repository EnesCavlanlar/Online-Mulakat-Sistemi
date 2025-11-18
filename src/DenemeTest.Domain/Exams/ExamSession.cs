using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams
{
    public class ExamSession : FullAuditedAggregateRoot<Guid>
    {
        public Guid TestId { get; protected set; }
        public Guid CandidateId { get; protected set; }

        /// <summary>Oturumun başladığı UTC zaman.</summary>
        public DateTime StartedAt { get; protected set; }

        /// <summary>Oturumun normal şekilde tamamlandığı UTC zaman (bitiş).</summary>
        public DateTime? FinishedAt { get; protected set; }

        public bool IsCancelled { get; protected set; }
        public string? CancelReason { get; protected set; }

        /// <summary>Proctoring ihlal sayısı (focus kaybı vb.).</summary>
        public int ViolationCount { get; protected set; }

        protected ExamSession()
        {
        }

        public ExamSession(Guid id, Guid testId, Guid candidateId, DateTime startedAt)
            : base(id)
        {
            TestId = testId;
            CandidateId = candidateId;
            StartedAt = startedAt;
            ViolationCount = 0;
        }

        /// <summary>Oturumu normal şekilde sonlandır.</summary>
        public void Finish(DateTime finishedAt)
        {
            FinishedAt = finishedAt;
        }

        /// <summary>Oturumu iptal et (ör: proctoring limit aşıldı).</summary>
        public void Cancel(string reason)
        {
            IsCancelled = true;
            CancelReason = reason;
        }

        /// <summary>Bir adet ihlal kaydet.</summary>
        public void RegisterViolation()
        {
            ViolationCount++;
        }

        /// <summary>İhlal sayacını sıfırla (gerekirse).</summary>
        public void ResetViolations()
        {
            ViolationCount = 0;
        }
    }
}
