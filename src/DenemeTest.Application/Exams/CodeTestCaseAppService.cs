using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DenemeTest.Application.Exams
{
    public class CodeTestCaseAppService : ApplicationService, ICodeTestCaseAppService
    {
        private readonly IRepository<CodeTestCase, Guid> _repo;

        public CodeTestCaseAppService(IRepository<CodeTestCase, Guid> repo)
        {
            _repo = repo;
        }

        public async Task<List<CodeTestCaseDto>> GetListByQuestionAsync(Guid questionId)
        {
            var list = await _repo.GetListAsync(x => x.QuestionId == questionId);

            return list
                .OrderBy(x => x.CreationTime)
                .Select(x => new CodeTestCaseDto
                {
                    Id = x.Id,
                    QuestionId = x.QuestionId,
                    Input = x.Input ?? string.Empty,
                    ExpectedOutput = x.ExpectedOutput ?? string.Empty,
                    Weight = x.Weight
                })
                .ToList();
        }

        public async Task ReplaceForQuestionAsync(Guid questionId, List<CodeTestCaseDto> items)
        {
            // Eski test-case'leri sil
            var existing = await _repo.GetListAsync(x => x.QuestionId == questionId);
            foreach (var e in existing)
            {
                await _repo.DeleteAsync(e, autoSave: true);
            }

            // Yeni yoksa çık
            if (items == null || items.Count == 0)
            {
                return;
            }

            // Yeni test-case'leri ekle
            foreach (var dto in items)
            {
                var entity = new CodeTestCase(
                    id: GuidGenerator.Create(),
                    questionId: questionId,
                    input: dto.Input ?? string.Empty,
                    expectedOutput: dto.ExpectedOutput ?? string.Empty,
                    weight: dto.Weight <= 0 ? 1 : dto.Weight
                );

                await _repo.InsertAsync(entity, autoSave: true);
            }
        }
    }
}
