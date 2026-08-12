using System.Text;

namespace EasyLotteryApi.Common;

internal static class HttpRequestBodyReader
{
    public static async Task<string> ReadTextAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
