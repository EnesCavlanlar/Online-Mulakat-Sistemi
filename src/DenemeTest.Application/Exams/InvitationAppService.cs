using System;
using System.Threading.Tasks;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;
using Microsoft.Extensions.Configuration;             // << EKLENDİ
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Guids;

namespace DenemeTest.Application.Exams;

public class InvitationAppService : ApplicationService, IInvitationAppService
{
    private readonly IRepository<ExamInvitation, Guid> _repo;
    private readonly IRepository<Candidate, Guid> _candidateRepo;
    private readonly IEmailSender _emailSender;
    private readonly IGuidGenerator _guid;
    private readonly IConfiguration _config;          // << EKLENDİ

    public InvitationAppService(
        IRepository<ExamInvitation, Guid> repo,
        IRepository<Candidate, Guid> candidateRepo,
        IEmailSender emailSender,
        IGuidGenerator guid,
        IConfiguration config)                        // << EKLENDİ
    {
        _repo = repo;
        _candidateRepo = candidateRepo;
        _emailSender = emailSender;
        _guid = guid;
        _config = config;                             // << EKLENDİ
    }

    public async Task<ExamInvitationDto> SendAsync(SendInvitationDto input)
    {
        // token üret
        var token = Guid.NewGuid().ToString("N");
        var inv = new ExamInvitation(_guid.Create(), input.TestId, input.CandidateId, token, input.ExpireAt);
        await _repo.InsertAsync(inv, autoSave: true);

        var candidate = await _candidateRepo.GetAsync(input.CandidateId);

        // Uygulama ana adresi: App:ClientUrl yoksa App:SelfUrl
        var baseUrl = _config["App:ClientUrl"] ?? _config["App:SelfUrl"] ?? "https://localhost:5001";
        var url = $"{baseUrl}/exam/start/{token}";

        // basit e-posta
        await _emailSender.SendAsync(
            to: candidate.Email,
            subject: "Sınav Daveti",
            body: $"Merhaba {candidate.FirstName},<br/>Sınavınıza girmek için bağlantı: <a href=\"{url}\">{url}</a>",
            isBodyHtml: true                           // << DOĞRU PARAM ADI
        );

        return ObjectMapper.Map<ExamInvitation, ExamInvitationDto>(inv);
    }
}

public interface IInvitationAppService
{
    Task<ExamInvitationDto> SendAsync(SendInvitationDto input);
}
