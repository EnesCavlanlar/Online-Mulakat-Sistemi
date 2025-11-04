using System;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DenemeTest.Application.Exams;

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

    public override async Task<QuestionDto> CreateAsync(CreateUpdateQuestionDto input)
    {
        var entity = ObjectMapper.Map<CreateUpdateQuestionDto, Question>(input);
        await Repository.InsertAsync(entity, autoSave: true);

        foreach (var o in input.Options)
            await _optionRepo.InsertAsync(new QuestionOption(GuidGenerator.Create(), entity.Id, o.Text, o.IsCorrect), true);

        var dto = ObjectMapper.Map<Question, QuestionDto>(entity);
        dto.Options = (await _optionRepo.GetListAsync(x => x.QuestionId == entity.Id))
            .Select(ObjectMapper.Map<QuestionOption, QuestionOptionDto>).ToList();

        return dto;
    }

    public override async Task<QuestionDto> UpdateAsync(Guid id, CreateUpdateQuestionDto input)
    {
        var entity = await Repository.GetAsync(id);

        // DTO -> Domain enum
        entity.Update((QuestionType)input.Type, input.Text, input.Points);

        await Repository.UpdateAsync(entity, true);

        var old = await _optionRepo.GetListAsync(x => x.QuestionId == id);
        foreach (var x in old)
            await _optionRepo.DeleteAsync(x);

        foreach (var o in input.Options)
            await _optionRepo.InsertAsync(new QuestionOption(GuidGenerator.Create(), id, o.Text, o.IsCorrect), true);

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
