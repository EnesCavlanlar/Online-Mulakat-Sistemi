using System;
using System.Collections.Generic;

namespace DenemeTest.Exams.Dtos
{
    public class SessionDetailDto
    {
        public Guid SessionId { get; set; }

        public Guid CandidateId { get; set; }

        public string CandidateFirstName { get; set; } = string.Empty;

        public string CandidateLastName { get; set; } = string.Empty;

        public string CandidateEmail { get; set; } = string.Empty;

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public int? Score { get; set; }

        public string? ScoreExplanation { get; set; }

        public int Violations { get; set; }

        public bool IsCancelled { get; set; }

        public string? CancelReason { get; set; }

        public string CandidateRecordingUrl { get; set; } = string.Empty;

        public string ScreenRecordingUrl { get; set; } = string.Empty;

        public string RecordingExistsUrl { get; set; } = string.Empty;

        public List<QuestionAnswerDetailDto> Answers { get; set; } = new();

        public List<CodeReviewDetailDto> CodeReviews { get; set; } = new();

        public List<ProctoringEventDetailDto> ProctoringEvents { get; set; } = new();
    }

    public class QuestionAnswerDetailDto
    {
        public Guid QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public string QuestionType { get; set; } = string.Empty;

        public int QuestionPoints { get; set; }

        public List<string>? SelectedOptions { get; set; }

        public List<string>? CorrectOptions { get; set; }

        public bool? IsCorrect { get; set; }

        public string? TextAnswer { get; set; }

        public string? CodeOutput { get; set; }
    }

    public class CodeReviewDetailDto
    {
        public Guid QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public bool TestsPassed { get; set; }

        public int PassedCount { get; set; }

        public int TotalCount { get; set; }

        public bool IsSuspicious { get; set; }

        public int? QualityScore { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string Flags { get; set; } = string.Empty;

        public string Provider { get; set; } = string.Empty;

        public DateTime CreationTime { get; set; }
    }

    public class ProctoringEventDetailDto
    {
        public Guid Id { get; set; }

        public string Type { get; set; } = string.Empty;

        public string? Detail { get; set; }

        public DateTime CreationTime { get; set; }
    }
}