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

        // QuestionOption
        CreateMap<QuestionOption, QuestionOptionDto>();
        CreateMap<CreateUpdateQuestionOptionDto, QuestionOption>();

        // Candidate
        CreateMap<Candidate, CandidateDto>();
        CreateMap<CreateUpdateCandidateDto, Candidate>();
    }
}
