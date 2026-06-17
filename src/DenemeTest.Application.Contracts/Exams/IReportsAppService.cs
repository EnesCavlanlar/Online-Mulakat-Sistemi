using System;
using System.Threading.Tasks;
using DenemeTest.Exams.Dtos;

namespace DenemeTest.Application.Exams
{
    public interface IReportsAppService
    {
        Task<LeaderboardItemDto[]> GetLeaderboardAsync(int take);

        Task<SessionDetailDto> GetSessionDetailAsync(Guid sessionId);

        Task DeleteSessionAsync(Guid sessionId);
    }
}