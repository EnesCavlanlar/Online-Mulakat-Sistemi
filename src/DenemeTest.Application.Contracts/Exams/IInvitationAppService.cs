using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Application.Dtos;
using DenemeTest.Exams.Dtos;

namespace DenemeTest.Exams;

public interface IInvitationAppService : IApplicationService
{
    // Tüm davetleri listeleme
    Task<PagedResultDto<ExamInvitationDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    // Belirli bir daveti getirme
    Task<ExamInvitationDto> GetAsync(Guid id);

    // Yeni davet oluşturma ve e-posta gönderme
    Task<ExamInvitationDto> CreateAsync(CreateExamInvitationDto input);

    // Daveti silme
    Task DeleteAsync(Guid id);
}
