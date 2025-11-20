using System;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace DenemeTest.Application.Exams
{
    public class QuestionAppService : CrudAppService<
        Question, QuestionDto, Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateQuestionDto, CreateUpdateQuestionDto>, IQuestionAppService
    {
        private readonly IRepository<QuestionOption, Guid> _optionRepo;

        public QuestionAppService(
            IRepository<Question, Guid> repository,
            IRepository<QuestionOption, Guid> optionRepo) : base(repository)
        {
            _optionRepo = optionRepo;
        }

        // DTO QuestionTypeDto -> Domain QuestionType (İSİME GÖRE, değerle değil)
        private static QuestionType MapToDomainType(QuestionTypeDto dtoType)
        {
            if (!Enum.TryParse<QuestionType>(dtoType.ToString(), out var result))
            {
                throw new UserFriendlyException($"Desteklenmeyen soru tipi: {dtoType}");
            }

            return result;
        }

        public override async Task<QuestionDto> CreateAsync(CreateUpdateQuestionDto input)
        {
            // AutoMapper yerine manuel, tipi isme göre map ediyoruz
            var domainType = MapToDomainType(input.Type);

            var entity = new Question(
                id: GuidGenerator.Create(),
                testId: input.TestId,
                type: domainType,
                text: input.Text,
                points: input.Points
            );

            await Repository.InsertAsync(entity, autoSave: true);

            // Çoktan seçmeli ise seçenekleri ekle
            foreach (var o in input.Options)
            {
                var option = new QuestionOption(
                    GuidGenerator.Create(),
                    entity.Id,
                    o.Text,
                    o.IsCorrect
                );

                await _optionRepo.InsertAsync(option, autoSave: true);
            }

            var dto = ObjectMapper.Map<Question, QuestionDto>(entity);
            dto.Options = (await _optionRepo.GetListAsync(x => x.QuestionId == entity.Id))
                .Select(ObjectMapper.Map<QuestionOption, QuestionOptionDto>)
                .ToList();

            return dto;
        }

        public override async Task<QuestionDto> UpdateAsync(Guid id, CreateUpdateQuestionDto input)
        {
            var entity = await Repository.GetAsync(id);

            // DTO -> Domain enum (İSİM bazlı, alt değerden bağımsız)
            var domainType = MapToDomainType(input.Type);

            // Entity içindeki Update metodu tüm alanları güncelliyor
            entity.Update(domainType, input.Text, input.Points);

            await Repository.UpdateAsync(entity, autoSave: true);

            // Eski seçenekleri sil
            var oldOptions = await _optionRepo.GetListAsync(x => x.QuestionId == id);
            foreach (var x in oldOptions)
            {
                await _optionRepo.DeleteAsync(x, autoSave: true);
            }

            // Yeni seçenekleri ekle
            foreach (var o in input.Options)
            {
                var option = new QuestionOption(
                    GuidGenerator.Create(),
                    id,
                    o.Text,
                    o.IsCorrect
                );

                await _optionRepo.InsertAsync(option, autoSave: true);
            }

            var dto = ObjectMapper.Map<Question, QuestionDto>(entity);
            dto.Options = (await _optionRepo.GetListAsync(x => x.QuestionId == id))
                .Select(ObjectMapper.Map<QuestionOption, QuestionOptionDto>)
                .ToList();

            return dto;
        }
    }

    public interface IQuestionAppService : ICrudAppService<
        QuestionDto, Guid, PagedAndSortedResultRequestDto,
        CreateUpdateQuestionDto, CreateUpdateQuestionDto>
    { }
}
