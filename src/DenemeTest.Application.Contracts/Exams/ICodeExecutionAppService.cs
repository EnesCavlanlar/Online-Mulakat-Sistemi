using System.Threading.Tasks;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Services;

namespace DenemeTest.Exams;

public interface ICodeExecutionAppService : IApplicationService
{
    /// <summary>
    /// Verilen kodu çalıştırır ve çıktı/hata bilgisini döner.
    /// Şimdilik gerçek sandbox değil, güvenli bir stub.
    /// </summary>
    Task<RunCodeResultDto> RunAsync(RunCodeRequestDto input);
}
