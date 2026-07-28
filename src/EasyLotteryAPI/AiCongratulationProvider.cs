using System.Net.Http.Headers;
using System.Net.Http.Json;
using EasyLotteryApplication.Settings;

namespace EasyLotteryApi;

public sealed class AiCongratulationProvider(IHttpClientFactory clients, IEasyLotteryConfigRepository settings)
{
    public async Task<string?> GenerateAsync(string donor, string prize, CancellationToken cancellationToken)
    {
        var key = (await settings.ReadAsync(cancellationToken)).SystemSettings.OpenAIKey?.Trim();
        if (string.IsNullOrWhiteSpace(key)) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = JsonContent.Create(new { model = "gpt-4.1-mini", messages = new[] { new { role = "user", content = $"用繁體中文寫一句不超過 40 字的直播抽獎恭喜語。得主：{donor}；獎項：{prize}。" } }, max_tokens = 80 });
            using var response = await clients.CreateClient().SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var payload = await response.Content.ReadFromJsonAsync<OpenAiResponse>(cancellationToken);
            return payload?.choices?.FirstOrDefault()?.message?.content?.Trim();
        }
        catch { return null; }
    }

    private sealed class OpenAiResponse { public List<OpenAiChoice>? choices { get; set; } }
    private sealed class OpenAiChoice { public OpenAiMessage? message { get; set; } }
    private sealed class OpenAiMessage { public string? content { get; set; } }
}
