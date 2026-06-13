using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace DenemeTest.Application.Exams
{
    public interface ICodeLlmReviewService
    {
        Task<CodeLlmReviewResult> ReviewAsync(CodeLlmReviewInput input);
    }

    public class CodeLlmReviewService : ICodeLlmReviewService, ITransientDependency
    {
        private readonly IConfiguration _configuration;

        public CodeLlmReviewService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<CodeLlmReviewResult> ReviewAsync(CodeLlmReviewInput input)
        {
            var enabled = _configuration.GetValue<bool>("LlmReview:Enabled");

            if (!enabled)
            {
                return CodeLlmReviewResult.Disabled();
            }

            var provider = _configuration["LlmReview:Provider"];

            if (string.IsNullOrWhiteSpace(provider))
            {
                provider = "OpenRouter";
            }

            // ÖNEMLİ:
            // Burada sadece user-secrets/appsettings içindeki OpenAI:ApiKey okunuyor.
            // OPENROUTER_API_KEY bilerek okunmuyor çünkü sende eski/yanlış env key kalmıştı.
            var apiKey = _configuration["OpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey) ||
                apiKey.Contains("BURAYA", StringComparison.OrdinalIgnoreCase))
            {
                return new CodeLlmReviewResult
                {
                    Enabled = true,
                    Available = false,
                    IsSuspicious = false,
                    QualityScore = null,
                    Summary = "LLM analizi yapılamadı. OpenAI:ApiKey user-secrets içinde tanımlı değil veya geçersiz görünüyor.",
                    Flags = Array.Empty<string>()
                };
            }

            var model = _configuration["OpenAI:Model"];

            if (string.IsNullOrWhiteSpace(model))
            {
                model = provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase)
                    ? "google/gemma-4-31b-it:free"
                    : "gpt-4o-mini";
            }

            var baseUrl = _configuration["OpenAI:BaseUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase)
                    ? "https://openrouter.ai/api/v1/chat/completions"
                    : "https://api.openai.com/v1/chat/completions";
            }

            var prompt = BuildPrompt(input);

            var payload = new OpenAiChatRequest
            {
                Model = model,
                Temperature = 0.1,
                Messages = new[]
                {
                    new OpenAiChatMessage
                    {
                        Role = "system",
                        Content = "Sen online teknik sınavlarda C# kod cevaplarını değerlendiren kıdemli bir yazılım mühendisisin. Sadece JSON döndür."
                    },
                    new OpenAiChatMessage
                    {
                        Role = "user",
                        Content = prompt
                    }
                }
            };

            try
            {
                using var client = new HttpClient();

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    baseUrl
                );

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                if (provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://localhost:44336");
                    request.Headers.TryAddWithoutValidation("X-Title", "DenemeTest Online Mulakat Sistemi");
                }

                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload, JsonOptions),
                    Encoding.UTF8,
                    "application/json"
                );

                using var response = await client.SendAsync(request);

                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new CodeLlmReviewResult
                    {
                        Enabled = true,
                        Available = false,
                        IsSuspicious = false,
                        QualityScore = null,
                        Summary = $"LLM analizi başarısız oldu. HTTP {(int)response.StatusCode}. Detay: {TrimForUser(responseText)}",
                        Flags = new[] { "llm_http_error" }
                    };
                }

                var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(responseText, JsonOptions);

                var content = chatResponse?.Choices != null && chatResponse.Choices.Length > 0
                    ? chatResponse.Choices[0].Message?.Content
                    : null;

                if (string.IsNullOrWhiteSpace(content))
                {
                    return new CodeLlmReviewResult
                    {
                        Enabled = true,
                        Available = false,
                        IsSuspicious = false,
                        QualityScore = null,
                        Summary = "LLM boş cevap döndürdü.",
                        Flags = new[] { "llm_empty_response" }
                    };
                }

                return ParseLlmJson(content);
            }
            catch (Exception ex)
            {
                return new CodeLlmReviewResult
                {
                    Enabled = true,
                    Available = false,
                    IsSuspicious = false,
                    QualityScore = null,
                    Summary = "LLM analizi sırasında hata oluştu: " + ex.Message,
                    Flags = new[] { "llm_exception" }
                };
            }
        }

        private static string BuildPrompt(CodeLlmReviewInput input)
        {
            return $$"""
            Aşağıdaki C# kod cevabını teknik sınav açısından değerlendir.

            Önemli kurallar:
            - Test-case sonucu ana karardır.
            - Kod testten geçiyorsa gereksiz yere yanlış deme.
            - Hard-code cevap basma, soruyu kandırma, alakasız çözüm, yasak/şüpheli kullanım varsa işaretle.
            - Adaya doğru cevabı söyleme.
            - Kısa ve net Türkçe açıklama yaz.
            - Sadece geçerli JSON döndür.
            - JSON dışında açıklama yazma.
            - Markdown kullanma.

            Döndürmen gereken JSON formatı:
            {
              "isSuspicious": false,
              "qualityScore": 85,
              "summary": "Kod testlerden geçti. Döngü ile çözüm yapılmış.",
              "flags": ["uses_loop"]
            }

            Soru:
            {{input.QuestionText}}

            Kod testlerden geçti mi:
            {{input.TestsPassed}}

            Geçen test sayısı:
            {{input.PassedCount}} / {{input.TotalCount}}

            Aday kodu:
            ```csharp
            {{input.Code}}
            ```
            """;
        }

        private static CodeLlmReviewResult ParseLlmJson(string content)
        {
            try
            {
                var cleaned = content.Trim();

                if (cleaned.StartsWith("```"))
                {
                    cleaned = cleaned
                        .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("```", "", StringComparison.OrdinalIgnoreCase)
                        .Trim();
                }

                var jsonStart = cleaned.IndexOf('{');
                var jsonEnd = cleaned.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    cleaned = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);
                }

                var parsed = JsonSerializer.Deserialize<CodeLlmReviewJson>(cleaned, JsonOptions);

                if (parsed == null)
                {
                    return new CodeLlmReviewResult
                    {
                        Enabled = true,
                        Available = false,
                        IsSuspicious = false,
                        QualityScore = null,
                        Summary = "LLM sonucu parse edilemedi.",
                        Flags = new[] { "llm_parse_failed" }
                    };
                }

                return new CodeLlmReviewResult
                {
                    Enabled = true,
                    Available = true,
                    IsSuspicious = parsed.IsSuspicious,
                    QualityScore = parsed.QualityScore,
                    Summary = parsed.Summary ?? "LLM analizi tamamlandı.",
                    Flags = parsed.Flags ?? Array.Empty<string>()
                };
            }
            catch
            {
                return new CodeLlmReviewResult
                {
                    Enabled = true,
                    Available = false,
                    IsSuspicious = false,
                    QualityScore = null,
                    Summary = "LLM cevabı JSON formatında değildi.",
                    Flags = new[] { "llm_invalid_json" }
                };
            }
        }

        private static string TrimForUser(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = text.Trim();

            return text.Length <= 500
                ? text
                : text.Substring(0, 500) + "...";
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public class CodeLlmReviewInput
    {
        public string QuestionText { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public bool TestsPassed { get; set; }

        public int PassedCount { get; set; }

        public int TotalCount { get; set; }
    }

    public class CodeLlmReviewResult
    {
        public bool Enabled { get; set; }

        public bool Available { get; set; }

        public bool IsSuspicious { get; set; }

        public int? QualityScore { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string[] Flags { get; set; } = Array.Empty<string>();

        public static CodeLlmReviewResult Disabled()
        {
            return new CodeLlmReviewResult
            {
                Enabled = false,
                Available = false,
                IsSuspicious = false,
                QualityScore = null,
                Summary = "LLM analizi kapalı.",
                Flags = Array.Empty<string>()
            };
        }
    }

    internal class CodeLlmReviewJson
    {
        public bool IsSuspicious { get; set; }

        public int? QualityScore { get; set; }

        public string? Summary { get; set; }

        public string[]? Flags { get; set; }
    }

    internal class OpenAiChatRequest
    {
        public string Model { get; set; } = "google/gemma-4-31b-it:free";

        public double Temperature { get; set; } = 0.1;

        public OpenAiChatMessage[] Messages { get; set; } = Array.Empty<OpenAiChatMessage>();
    }

    internal class OpenAiChatMessage
    {
        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

    internal class OpenAiChatResponse
    {
        public OpenAiChatChoice[]? Choices { get; set; }
    }

    internal class OpenAiChatChoice
    {
        public OpenAiChatMessage? Message { get; set; }
    }
}