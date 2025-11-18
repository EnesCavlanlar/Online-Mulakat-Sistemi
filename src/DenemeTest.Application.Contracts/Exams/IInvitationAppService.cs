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

    // Sadece daveti oluşturur (e-posta göndermez)
    Task<ExamInvitationDto> CreateAsync(CreateExamInvitationDto input);

    // Daveti oluşturur ve e-posta gönderir
    Task<ExamInvitationDto> CreateAndSendAsync(CreateExamInvitationDto input);

    // Mevcut bir davet için e-posta gönderme / yeniden gönderme
    Task SendEmailAsync(Guid invitationId);

    // Daveti silme
    Task DeleteAsync(Guid id);
}
