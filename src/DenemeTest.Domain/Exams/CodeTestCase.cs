using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams
{
    public class CodeTestCase : FullAuditedAggregateRoot<Guid>
    {
        public Guid QuestionId { get; set; }
        public string? Input { get; set; }
        public string? ExpectedOutput { get; set; }
        public int Weight { get; set; }

        protected CodeTestCase()
        {
        }

        public CodeTestCase(Guid id, Guid questionId, string? input, string? expectedOutput, int weight)
            : base(id)
        {
            QuestionId = questionId;
            Input = input;
            ExpectedOutput = expectedOutput;
            Weight = weight <= 0 ? 1 : weight;
        }
    }
}
