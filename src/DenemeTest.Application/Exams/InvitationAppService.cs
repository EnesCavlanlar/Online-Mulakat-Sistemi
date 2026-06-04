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
            items.Select(x => MapToDto(x, rawToken: null)).ToList()
        );
    }

    public async Task<ExamInvitationDto> GetAsync(Guid id)
    {
        var e = await _invRepo.GetAsync(id);
        return MapToDto(e, rawToken: null);
    }

    public async Task<ExamInvitationDto> CreateAsync(CreateExamInvitationDto input)
    {
        if (input.ExpireAt <= DateTime.UtcNow)
        {
            throw new BusinessException("Invitation:ExpireAtInvalid")
                .WithData("ExpireAt", input.ExpireAt);
        }

        var candidate = await _candRepo.GetAsync(input.CandidateId);
        var test = await _testRepo.GetAsync(input.TestId);

        var rawToken = await GenerateUniqueRawTokenAsync();
        var tokenHash = HashToken(rawToken);

        var entity = new ExamInvitation(
            id: GuidGenerator.Create(),
            testId: test.Id,
            candidateId: candidate.Id,
            tokenHash: tokenHash,
            expireAt: input.ExpireAt.ToUniversalTime()
        );

        await _invRepo.InsertAsync(entity, autoSave: true);

        // Raw token sadece bu response içinde bir kere döner.
        // Listeleme ve detay ekranında tekrar gösterilmez.
        return MapToDto(entity, rawToken);
    }

    public async Task<ExamInvitationDto> CreateAndSendAsync(CreateExamInvitationDto input)
    {
        if (input.ExpireAt <= DateTime.UtcNow)
        {
            throw new BusinessException("Invitation:ExpireAtInvalid")
                .WithData("ExpireAt", input.ExpireAt);
        }

        var candidate = await _candRepo.GetAsync(input.CandidateId);
        var test = await _testRepo.GetAsync(input.TestId);

        if (string.IsNullOrWhiteSpace(candidate.Email))
        {
            throw new BusinessException("Invitation:CandidateEmailMissing")
                .WithData("CandidateId", candidate.Id);
        }

        var rawToken = await GenerateUniqueRawTokenAsync();
        var tokenHash = HashToken(rawToken);

        var entity = new ExamInvitation(
            id: GuidGenerator.Create(),
            testId: test.Id,
            candidateId: candidate.Id,
            tokenHash: tokenHash,
            expireAt: input.ExpireAt.ToUniversalTime()
        );

        await _invRepo.InsertAsync(entity, autoSave: true);

        await SendEmailWithRawTokenAsync(entity, candidate, test, rawToken);

        entity.MarkSent();
        await _invRepo.UpdateAsync(entity, autoSave: true);

        return MapToDto(entity, rawToken: null);
    }

    public Task SendEmailAsync(Guid invitationId)
    {
        throw new UserFriendlyException(
            "Hash'li token sisteminde davet linki sonradan tekrar üretilemez. Lütfen daveti CreateAndSend ile oluşturup gönderin."
        );
    }

    public async Task DeleteAsync(Guid id)
    {
        await _invRepo.DeleteAsync(id);
    }

    public async Task<BulkInvitationResultDto> CreateBulkAsync(BulkExamInvitationDto input)
    {
        var result = new BulkInvitationResultDto();

        await _testRepo.GetAsync(input.TestId);

        foreach (var candidateId in input.CandidateIds)
        {
            var item = new BulkInvitationItemResultDto
            {
                CandidateId = candidateId
            };

            try
            {
                var createDto = new CreateExamInvitationDto
                {
                    TestId = input.TestId,
                    CandidateId = candidateId,
                    ExpireAt = input.ExpireAt
                };

                var inv = await CreateAndSendAsync(createDto);

                item.InvitationId = inv.Id;
                item.Success = true;
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                item.Success = false;
                item.Error = ex.Message;
                result.FailureCount++;
            }

            result.Items.Add(item);
        }

        return result;
    }

    private async Task SendEmailWithRawTokenAsync(
        ExamInvitation invitation,
        Candidate candidate,
        Test test,
        string rawToken)
    {
        if (invitation.ExpireAt <= DateTime.UtcNow)
        {
            throw new BusinessException("Invitation:AlreadyExpired")
                .WithData("ExpireAt", invitation.ExpireAt);
        }

        var baseUrl =
            _config["App:ClientUrl"]
            ?? _config["App:SelfUrl"]
            ?? _config["App:BaseUrl"]
            ?? "https://localhost:44336";

        var link = $"{baseUrl.TrimEnd('/')}/api/exam/start/{rawToken}";

        var subject = $"Sınav Daveti – {test.Name}";

        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Merhaba {candidate.FirstName} {candidate.LastName},");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine($"\"{test.Name}\" sınavına davet edildiniz.");
        bodyBuilder.AppendLine($"Son geçerlilik: {invitation.ExpireAt:G}");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Tek kullanımlık giriş bağlantınız:");
        bodyBuilder.AppendLine(link);
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Not: Bu bağlantı süre dolduğunda veya kullanıldığında geçersiz olacaktır.");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("İyi çalışmalar dileriz.");

        await _emailSender.SendAsync(
            to: candidate.Email,
            subject: subject,
            body: bodyBuilder.ToString(),
            isBodyHtml: false
        );
    }

    private async Task<string> GenerateUniqueRawTokenAsync()
    {
        while (true)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            var tokenHash = HashToken(rawToken);

            if (!await _invRepo.AnyAsync(x => x.TokenHash == tokenHash))
            {
                return rawToken;
            }
        }
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static ExamInvitationDto MapToDto(ExamInvitation e, string? rawToken)
    {
        return new ExamInvitationDto
        {
            Id = e.Id,
            TestId = e.TestId,
            CandidateId = e.CandidateId,
            Token = rawToken,
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
}