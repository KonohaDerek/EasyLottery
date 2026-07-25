using System.Security.Cryptography;
using System.Text;

namespace EasyLotteryApi;

public sealed class AdminAccess
{
    public const string HeaderName = "X-EasyLottery-Admin-Token";
    private readonly string _token;

    public AdminAccess(IConfiguration configuration) => _token = configuration["Settings:AdminToken"]?.Trim() ?? "";

    public bool IsAuthorized(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(_token)) return false;
        var supplied = request.Headers[HeaderName].ToString();
        return supplied.Length == _token.Length && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(_token));
    }
}
