using System;

namespace DenemeTest.Exams.Dtos
{
    public class TestCaseResultDto
    {
        public Guid TestCaseId { get; set; }

        public string? Input { get; set; }

        public string? ExpectedOutput { get; set; }

        public string? ActualOutput { get; set; }

        public string? Error { get; set; }

        public int ExitCode { get; set; }

        public bool IsSuccess { get; set; }
    }
}
