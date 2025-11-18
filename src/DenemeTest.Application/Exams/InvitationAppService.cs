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

    /// <summary>
    /// Sadece daveti oluşturur, e-posta GÖNDERMEZ.
    /// Mail göndermek için SendEmailAsync veya CreateAndSendAsync kullanılmalıdır.
    /// </summary>
    public async Task<ExamInvitationDto> CreateAsync(CreateExamInvitationDto input)
    {
        if (input.ExpireAt <= DateTime.UtcNow)
            throw new BusinessException("Invitation:ExpireAtInvalid")
                .WithData("ExpireAt", input.ExpireAt);

        var candidate = await _candRepo.GetAsync(input.CandidateId);
        var test = await _testRepo.GetAsync(input.TestId);

        var token = await GenerateUniqueTokenAsync();

        var entity = new ExamInvitation(
            id: GuidGenerator.Create(),
            testId: test.Id,
            candidateId: candidate.Id,
            token: token,
            expireAt: input.ExpireAt.ToUniversalTime()
        );

        await _invRepo.InsertAsync(entity, autoSave: true);

        return MapToDto(entity);
    }

    /// <summary>
    /// Daveti oluşturur ve adayın e-posta adresine davet mailini gönderir.
    /// </summary>
    public async Task<ExamInvitationDto> CreateAndSendAsync(CreateExamInvitationDto input)
    {
        var dto = await CreateAsync(input);
        await SendEmailAsync(dto.Id);

        // SentAt gibi alanlar güncelleneceği için son halini tekrar yükleyip döndürüyoruz.
        var updated = await _invRepo.GetAsync(dto.Id);
        return MapToDto(updated);
    }

    /// <summary>
    /// Mevcut bir davet için e-posta gönderir (veya yeniden gönderir).
    /// </summary>
    public async Task SendEmailAsync(Guid invitationId)
    {
        var invitation = await _invRepo.GetAsync(invitationId);
        var candidate = await _candRepo.GetAsync(invitation.CandidateId);
        var test = await _testRepo.GetAsync(invitation.TestId);

        if (string.IsNullOrWhiteSpace(candidate.Email))
        {
            throw new BusinessException("Invitation:CandidateEmailMissing")
                .WithData("CandidateId", candidate.Id);
        }

        if (invitation.ExpireAt <= DateTime.UtcNow)
        {
            throw new BusinessException("Invitation:AlreadyExpired")
                .WithData("ExpireAt", invitation.ExpireAt);
        }

        // Base URL tercih sırası: App:ClientUrl -> App:SelfUrl -> App:BaseUrl
        var baseUrl =
            _config["App:ClientUrl"]
            ?? _config["App:SelfUrl"]
            ?? _config["App:BaseUrl"]
            ?? "https://localhost:44336";

        var link = $"{baseUrl.TrimEnd('/')}/exam/start/{invitation.Token}";

        var subject = $"Sınav Daveti – {test.Name}";

        // Plain-text gövde
        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Merhaba {candidate.FirstName} {candidate.LastName},");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine($"\"{test.Name}\" sınavına davet edildiniz.");
        bodyBuilder.AppendLine($"Son geçerlilik: {invitation.ExpireAt:G}");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Tek kullanımlık giriş bağlantınız:");
        bodyBuilder.AppendLine(link);
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Not: Bu bağlantı süre dolduğunda geçersiz olacaktır.");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("İyi çalışmalar dileriz.");

        var bodyText = bodyBuilder.ToString();

        await _emailSender.SendAsync(
            to: candidate.Email,
            subject: subject,
            body: bodyText,
            isBodyHtml: false // plain-text
        );

        invitation.MarkSent();
        await _invRepo.UpdateAsync(invitation, autoSave: true);
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
