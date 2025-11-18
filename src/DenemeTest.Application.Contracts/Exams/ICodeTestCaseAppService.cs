using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using DenemeTest.Exams.Dtos;

namespace DenemeTest.Exams
{
    public interface ICodeTestCaseAppService : IApplicationService
    {
        /// <summary>Belirli bir soruya ait tüm test-case'leri döner.</summary>
        Task<List<CodeTestCaseDto>> GetListByQuestionAsync(Guid questionId);

        /// <summary>Verilen soruya ait tüm test-case'leri baştan yazar (eski kayıtları siler).</summary>
        Task ReplaceForQuestionAsync(Guid questionId, List<CodeTestCaseDto> items);
    }
}
