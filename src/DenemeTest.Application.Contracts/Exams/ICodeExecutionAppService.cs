using System;
using System.Threading.Tasks;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Services;

namespace DenemeTest.Exams
{
    public interface ICodeExecutionAppService : IApplicationService
    {
        /// <summary>
        /// Sorunun test caselerine göre kodu çalıştırır,
        /// her test case için stdout/stderr/başarı durumunu döner.
        /// </summary>
        Task<RunCodeResultDto> RunAsync(RunCodeRequestDto input);
    }
}
