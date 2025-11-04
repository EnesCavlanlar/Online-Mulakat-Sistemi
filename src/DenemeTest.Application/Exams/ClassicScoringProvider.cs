using System.Threading.Tasks;

namespace DenemeTest.Application.Exams;

// Gerçek AI entegrasyonu için bu arayüzü koruyoruz.
public interface IClassicScoringProvider
{
    Task<(int score, string note)> ScoreAsync(string questionText, string candidateText);
}

// Stub: Anahtar kelime sayımına göre kaba puan (demo)
public class ClassicScoringStub : IClassicScoringProvider
{
    public Task<(int score, string note)> ScoreAsync(string questionText, string candidateText)
    {
        if (string.IsNullOrWhiteSpace(candidateText))
            return Task.FromResult((0, "Boş cevap"));

        // çok basit: cevap uzunluğu 0-300 arası normalize
        var len = candidateText.Trim().Length;
        var sc = len >= 300 ? 100 : (int)(len / 3.0); // 300 char -> 100 puan
        return Task.FromResult((sc, $"Stub: {len} karakter cevap"));
    }
}
