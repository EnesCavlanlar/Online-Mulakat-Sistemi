using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace DenemeTest.Application.Exams;

public class TestAppService : CrudAppService<
    Test, TestDto, Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateTestDto, CreateUpdateTestDto>, ITestAppService
{
    private readonly IRepository<Question, Guid> _questionRepo;
    private readonly IRepository<QuestionOption, Guid> _optionRepo;
    private readonly IRepository<CodeTestCase, Guid> _codeTestCaseRepo;
    private readonly IRepository<ExamInvitation, Guid> _invitationRepo;
    private readonly IRepository<ExamSession, Guid> _sessionRepo;
    private readonly IRepository<Answer, Guid> _answerRepo;
    private readonly IRepository<Score, Guid> _scoreRepo;
    private readonly IRepository<ProctoringEvent, Guid> _proctoringEventRepo;
    private readonly IRepository<CodeReview, Guid> _codeReviewRepo;

    public TestAppService(
        IRepository<Test, Guid> repository,
        IRepository<Question, Guid> questionRepo,
        IRepository<QuestionOption, Guid> optionRepo,
        IRepository<CodeTestCase, Guid> codeTestCaseRepo,
        IRepository<ExamInvitation, Guid> invitationRepo,
        IRepository<ExamSession, Guid> sessionRepo,
        IRepository<Answer, Guid> answerRepo,
        IRepository<Score, Guid> scoreRepo,
        IRepository<ProctoringEvent, Guid> proctoringEventRepo,
        IRepository<CodeReview, Guid> codeReviewRepo)
        : base(repository)
    {
        _questionRepo = questionRepo;
        _optionRepo = optionRepo;
        _codeTestCaseRepo = codeTestCaseRepo;
        _invitationRepo = invitationRepo;
        _sessionRepo = sessionRepo;
        _answerRepo = answerRepo;
        _scoreRepo = scoreRepo;
        _proctoringEventRepo = proctoringEventRepo;
        _codeReviewRepo = codeReviewRepo;
    }

    protected override async Task<IQueryable<Test>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await Repository.WithDetailsAsync(x => x.Questions);

        return query
            .OrderByDescending(x => x.CreationTime)
            .ThenBy(x => x.Name);
    }

    public override async Task<TestDto> CreateAsync(CreateUpdateTestDto input)
    {
        NormalizeInput(input);
        ValidateInput(input);

        return await base.CreateAsync(input);
    }

    public override async Task<TestDto> UpdateAsync(Guid id, CreateUpdateTestDto input)
    {
        if (id == Guid.Empty)
        {
            throw new UserFriendlyException("Güncellenecek test bulunamadı.");
        }

        NormalizeInput(input);
        ValidateInput(input);

        return await base.UpdateAsync(id, input);
    }

    [UnitOfWork]
    public override async Task DeleteAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new UserFriendlyException("Silinecek test bulunamadı.");
        }

        var test = await Repository.GetAsync(id);

        var questions = await _questionRepo.GetListAsync(q => q.TestId == id);
        var questionIds = questions.Select(q => q.Id).Distinct().ToList();

        var sessions = await _sessionRepo.GetListAsync(s => s.TestId == id);
        var sessionIds = sessions.Select(s => s.Id).Distinct().ToList();

        await DeleteCodeReviewsAsync(sessionIds);
        await DeleteProctoringEventsAsync(sessionIds);
        await DeleteScoresAsync(sessionIds);
        await DeleteAnswersAsync(sessionIds, questionIds);
        await DeleteInvitationsAsync(id);
        await DeleteSessionsAsync(sessions);
        await DeleteCodeTestCasesAsync(questionIds);
        await DeleteOptionsAsync(questionIds);
        await DeleteQuestionsAsync(questions);

        await Repository.DeleteAsync(test, autoSave: true);
    }

    private async Task DeleteCodeReviewsAsync(List<Guid> sessionIds)
    {
        if (sessionIds.Count == 0)
        {
            return;
        }

        var items = await _codeReviewRepo.GetListAsync(x => sessionIds.Contains(x.ExamSessionId));

        foreach (var item in items)
        {
            await _codeReviewRepo.DeleteAsync(item, autoSave: false);
        }
    }

    private async Task DeleteProctoringEventsAsync(List<Guid> sessionIds)
    {
        if (sessionIds.Count == 0)
        {
            return;
        }

        var items = await _proctoringEventRepo.GetListAsync(x => sessionIds.Contains(x.ExamSessionId));

        foreach (var item in items)
        {
            await _proctoringEventRepo.DeleteAsync(item, autoSave: false);
        }
    }

    private async Task DeleteScoresAsync(List<Guid> sessionIds)
    {
        if (sessionIds.Count == 0)
        {
            return;
        }

        var items = await _scoreRepo.GetListAsync(x => sessionIds.Contains(x.ExamSessionId));

        foreach (var item in items)
        {
            await _scoreRepo.DeleteAsync(item, autoSave: false);
        }
    }

    private async Task DeleteAnswersAsync(List<Guid> sessionIds, List<Guid> questionIds)
    {
        if (sessionIds.Count == 0 && questionIds.Count == 0)
        {
            return;
        }

        var items = await _answerRepo.GetListAsync(x =>
            sessionIds.Contains(x.ExamSessionId) ||
            questionIds.Contains(x.QuestionId));

        foreach (var item in items)
        {
            await _answerRepo.DeleteAsync(item, autoSave: false);
        }
    }

    private async Task DeleteInvitationsAsync(Guid testId)
    {
        var items = await _invitationRepo.GetListAsync(x => x.TestId == testId);

        foreach (var item in items)
        {
            await _invitationRepo.DeleteAsync(item, autoSave: false);
        }
    }

    private async Task DeleteSessionsAsync(List<ExamSession> sessions)
    {
        foreach (var item in sessions)
        {
            await _sessionRepo.DeleteAsync(item, autoSave: false);
        }
    }

    private async Task DeleteCodeTestCasesAsync(List<Guid> questionIds)
    {
        if (questionIds.Count == 0)
        {
            return;
        }

        var items = await _codeTestCaseRepo.GetListAsync(x => questionIds.Contains(x.QuestionId));

        foreach (var item in items)
        {
            await _codeTestCaseRepo.DeleteAsync(item, autoSave: false);
        }
    }

    private async Task DeleteOptionsAsync(List<Guid> questionIds)
    {
        if (questionIds.Count == 0)
        {
            return;
        }

        var items = await _optionRepo.GetListAsync(x => questionIds.Contains(x.QuestionId));

        foreach (var item in items)
        {
            await _optionRepo.DeleteAsync(item, autoSave: false);
        }
    }

    private async Task DeleteQuestionsAsync(List<Question> questions)
    {
        foreach (var item in questions)
        {
            await _questionRepo.DeleteAsync(item, autoSave: false);
        }
    }

    private static void NormalizeInput(CreateUpdateTestDto input)
    {
        input.Name = input.Name?.Trim() ?? string.Empty;
        input.Description = input.Description?.Trim();

        if (input.DurationMinutes <= 0)
        {
            input.DurationMinutes = 60;
        }

        if (input.PassScore < 0)
        {
            input.PassScore = 0;
        }

        if (input.PassScore > 100)
        {
            input.PassScore = 100;
        }
    }

    private static void ValidateInput(CreateUpdateTestDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("Test adı boş olamaz.");
        }

        if (input.DurationMinutes <= 0)
        {
            throw new UserFriendlyException("Test süresi 1 dakika veya daha fazla olmalı.");
        }

        if (input.PassScore < 0 || input.PassScore > 100)
        {
            throw new UserFriendlyException("Geçme puanı 0 ile 100 arasında olmalı.");
        }

        if (input.StartAt.HasValue &&
            input.EndAt.HasValue &&
            input.EndAt.Value < input.StartAt.Value)
        {
            throw new UserFriendlyException("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        }
    }
}

public interface ITestAppService : ICrudAppService<
    TestDto, Guid, PagedAndSortedResultRequestDto,
    CreateUpdateTestDto, CreateUpdateTestDto>
{
}