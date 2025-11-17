using System;
using System.Threading.Tasks;
using DenemeTest.Exams.Dtos;

namespace DenemeTest.Exams
{
    public interface IExamRunAppService
    {
        // Token ile oturum başlatma
        Task<StartWithTokenResultDto> StartWithTokenAsync(string token);

        // Sınavı koşu için getir (sorular + seçenekler)
        Task<TestRunDto> GetTestForRunAsync(Guid sessionId);

        // Cevap kaydet (MCQ + klasik + coding hepsi buradan gidiyor)
        Task SubmitAnswerAsync(SubmitAnswerDto input);

        // Puan hesapla ve kaydet
        Task<int> ComputeAndSaveScoreAsync(Guid sessionId);
    }
}
