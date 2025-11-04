using System;
using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
//using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DenemeTest.Application.Exams;

public class TestAppService : CrudAppService<
    Test, TestDto, Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateTestDto, CreateUpdateTestDto>, ITestAppService
{
    public TestAppService(IRepository<Test, Guid> repository) : base(repository) { }

    protected override async Task<IQueryable<Test>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await Repository.WithDetailsAsync(x => x.Questions);
        return query;
    }
}

public interface ITestAppService : ICrudAppService<
    TestDto, Guid, PagedAndSortedResultRequestDto,
    CreateUpdateTestDto, CreateUpdateTestDto>
{ }
