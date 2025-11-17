using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;

namespace DenemeTest.Application.Exams;

public class InvitationAppService : ApplicationService, IInvitationAppService
{
    private readonly IRepository<ExamInvitation, Guid> _invRepo;
    private readonly IRepository<Candidate, Guid> _candRepo;
    private readonly IRepository<Test, Guid> _testRepo;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;

    public InvitationAppService(
        IRepository<ExamInvitation, Guid> invRepo,
        IRepository<Candidate, Guid> candRepo,
        IRepository<Test, Guid> testRepo,
        IEmailSender emailSender,
        IConfiguration config)
    {
        _invRepo = invRepo;
        _candRepo = candRepo;
        _testRepo = testRepo;
        _emailSender = emailSender;
        _config = config;
    }

    // -------------------- Queries --------------------

    public async Task<PagedResultDto<ExamInvitationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var q = await _invRepo.GetQueryableAsync();

        var total = q.LongCount();

        var items = q
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount == 0 ? 10 : input.MaxResultCount)
            .ToList();

        return new PagedResultDto<ExamInvitationDto>(
            total,
            items.Select(MapToDto).ToList()
        );
    }

    public async Task<ExamInvitationDto> GetAsync(Guid id)
    {
        var e = await _invRepo.GetAsync(id);
        return MapToDto(e);
    }

    // -------------------- Commands --------------------

    public async Task<ExamInvitationDto> CreateAsync(CreateExamInvitationDto input)
    {
        if (input.ExpireAt <= DateTime.UtcNow)
            throw new BusinessException("Invitation:ExpireAtInvalid")
                .WithData("ExpireAt", input.ExpireAt);

        var candidate = await _candRepo.GetAsync(input.CandidateId);
        var test = await _testRepo.GetAsync(input.TestId);

        // Güvenli, URL-safe token üret
        var token = await GenerateUniqueTokenAsync();

        var entity = new ExamInvitation(
            id: GuidGenerator.Create(),
            testId: test.Id,
            candidateId: candidate.Id,
            token: token,
            expireAt: input.ExpireAt.ToUniversalTime()
        );

        await _invRepo.InsertAsync(entity, autoSave: true);

        // Base URL tercih sırası: App:ClientUrl -> App:SelfUrl -> App:BaseUrl
        var baseUrl = _config["App:ClientUrl"] ?? _config["App:SelfUrl"] ?? _config["App:BaseUrl"] ?? "https://localhost:44336";

        // Notlarımızda belirlediğimiz rota:
        var link = $"{baseUrl.TrimEnd('/')}/exam/start/{token}";

        var subject = $"Sınav Daveti – {test.Name}";
        var bodyHtml = new StringBuilder()
            .Append($"Merhaba {candidate.FirstName} {candidate.LastName},<br/><br/>")
            .Append($"<b>{test.Name}</b> sınavına davet edildiniz.<br/>")
            .Append($"Son geçerlilik: {input.ExpireAt:G}<br/><br/>")
            .Append($"Tek kullanımlık giriş bağlantınız: <a href=\"{link}\">{link}</a><br/><br/>")
            .Append("Not: Bu bağlantı süre dolduğunda geçersiz olacaktır.")
            .ToString();

        await _emailSender.SendAsync(
            to: candidate.Email,
            subject: subject,
            body: bodyHtml,
            isBodyHtml: true
        );

        entity.MarkSent();
        await _invRepo.UpdateAsync(entity, autoSave: true);

        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _invRepo.DeleteAsync(id);
    }

    // -------------------- Helpers --------------------

    private async Task<string> GenerateUniqueTokenAsync()
    {
        static string Make() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        while (true)
        {
            var t = Make();
            if (!await _invRepo.AnyAsync(x => x.Token == t))
                return t;
        }
    }

    private static ExamInvitationDto MapToDto(ExamInvitation e)
        => new ExamInvitationDto
        {
            Id = e.Id,
            TestId = e.TestId,
            CandidateId = e.CandidateId,
            Token = e.Token,
            ExpireAt = e.ExpireAt,
            SentAt = e.SentAt,
            UsedAt = e.UsedAt,
            IsUsed = e.IsUsed,
            CreationTime = e.CreationTime,
            CreatorId = e.CreatorId,
            LastModificationTime = e.LastModificationTime,
            LastModifierId = e.LastModifierId
        };
}
