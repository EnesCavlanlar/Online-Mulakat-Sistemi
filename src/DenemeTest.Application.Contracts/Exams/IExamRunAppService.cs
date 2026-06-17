using System;
using System.Threading.Tasks;
using DenemeTest.Exams.Dtos;

namespace DenemeTest.Exams
{
    public interface IExamRunAppService
    {
        Task<StartWithTokenResultDto> StartWithTokenAsync(string token);

        Task<TestRunDto> GetTestForRunAsync(Guid sessionId);

        Task SubmitAnswerAsync(SubmitAnswerDto input);

        Task<int> ComputeAndSaveScoreAsync(Guid sessionId);
    }
}