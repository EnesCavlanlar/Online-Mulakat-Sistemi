using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Services;

namespace DenemeTest.Exams
{
    public interface ICodeTestCaseAppService : IApplicationService
    {
        /// <summary>
        /// Belirli bir soruya ait tüm test-case'leri getirir.
        /// </summary>
        Task<List<CodeTestCaseDto>> GetListByQuestionAsync(Guid questionId);

        /// <summary>
        /// Verilen soruya ait mevcut tüm test-case'leri siler
        /// ve verilen listeyi yeniden yazar.
        /// </summary>
        Task ReplaceForQuestionAsync(Guid questionId, List<CodeTestCaseDto> items);
    }
}
