using AutoMapper;
using DenemeTest.Exams;
using DenemeTest.Exams.Dtos;

namespace DenemeTest;

public class DenemeTestApplicationAutoMapperProfile : Profile
{
    public DenemeTestApplicationAutoMapperProfile()
    {
        // Test
        CreateMap<Test, TestDto>();
        CreateMap<CreateUpdateTestDto, Test>();

        // Question
        CreateMap<Question, QuestionDto>();
        CreateMap<CreateUpdateQuestionDto, Question>();

        // 🔥 CodeTestCase Map – ReverseMap kaldırıldı, Id IGNORE EDİLDİ
        CreateMap<CodeTestCase, CodeTestCaseDto>();

        CreateMap<CodeTestCaseDto, CodeTestCase>()
            .ForMember(x => x.Id, opt => opt.Ignore());  // <-- ÇÖZÜM
                                                         // Not: Id hiçbir zaman DTO’dan gelmeyecek, EF yeni Guid oluşturacak.

        // QuestionOption
        CreateMap<QuestionOption, QuestionOptionDto>();
        CreateMap<CreateUpdateQuestionOptionDto, QuestionOption>();

        // Candidate
        CreateMap<Candidate, CandidateDto>();
        CreateMap<CreateUpdateCandidateDto, Candidate>();
    }
}
