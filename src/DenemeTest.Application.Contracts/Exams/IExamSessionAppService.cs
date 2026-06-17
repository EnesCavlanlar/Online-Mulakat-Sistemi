using System;
using System.Threading.Tasks;
using DenemeTest.Exams.Dtos;

namespace DenemeTest.Application.Exams
{
    public interface IExamSessionAppService
    {
        Task<StartByTokenResultDto> StartByTokenAsync(string token);

        Task RecordEventAsync(Guid sessionId, ProctoringEventTypeDto type, string? detail);

        Task CancelAsync(Guid sessionId, string reason);

        Task FinishAsync(Guid sessionId, int? scoreValue = null, string? scoreNote = null);
    }
}