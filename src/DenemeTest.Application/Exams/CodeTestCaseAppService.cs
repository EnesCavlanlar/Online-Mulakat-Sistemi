using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DenemeTest.Application.Exams
{
    /// <summary>
    /// Coding sorularına ait test-case yönetimi.
    /// Admin / Sorular ekranındaki "Kodlama Test Case'leri"
    /// bu servis üzerinden çalışır.
    /// </summary>
    public class CodeTestCaseAppService : ApplicationService, ICodeTestCaseAppService
    {
        private readonly IRepository<CodeTestCase, Guid> _repo;

        public CodeTestCaseAppService(IRepository<CodeTestCase, Guid> repo)
        {
            _repo = repo;
        }

        public async Task<List<CodeTestCaseDto>> GetListByQuestionAsync(Guid questionId)
        {
            if (questionId == Guid.Empty)
                throw new UserFriendlyException("QuestionId boş olamaz.");

            var entities = await _repo.GetListAsync(x => x.QuestionId == questionId);

            return ObjectMapper.Map<List<CodeTestCase>, List<CodeTestCaseDto>>(entities);
        }

        public async Task ReplaceForQuestionAsync(Guid questionId, List<CodeTestCaseDto> items)
        {
            if (questionId == Guid.Empty)
                throw new UserFriendlyException("QuestionId boş olamaz.");

            // 1) Eski test-case'leri sil
            var existing = await _repo.GetListAsync(x => x.QuestionId == questionId);
            if (existing.Any())
            {
                await _repo.DeleteManyAsync(existing, autoSave: true);
            }

            // 2) Yeni gelenleri ekle
            foreach (var dto in items)
            {
                // DTO -> Entity map
                var entity = ObjectMapper.Map<CodeTestCaseDto, CodeTestCase>(dto);

                // Soru ile ilişkilendir
                entity.QuestionId = questionId;

                // Id'yi burada elle set etmiyoruz; EF/ABP bunu halledecek
                // (Guid default ise GuidGenerator/DB üzerinden üretilecek)

                await _repo.InsertAsync(entity, autoSave: true);
            }
        }
    }
}
