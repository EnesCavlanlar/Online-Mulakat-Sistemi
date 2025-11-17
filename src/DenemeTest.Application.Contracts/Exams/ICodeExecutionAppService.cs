using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using DenemeTest.Exams.Dtos;

namespace DenemeTest.Exams
{
    public interface ICodeExecutionAppService : IApplicationService
    {
        Task<RunCodeResultDto> RunAsync(RunCodeRequestDto input);
    }
}
