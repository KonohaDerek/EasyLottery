namespace EasyLotteryWasm.Layout;

/// <summary>集中處理 legacy route 導回管理頁時使用的簡單 query 參數。</summary>
public static class NavigationQuery
{
    public static bool TryGetInt(string query, string key, out int value)
    {
        value = 0;
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && string.Equals(Uri.UnescapeDataString(pair[0]), key, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(Uri.UnescapeDataString(pair[1]), out value))
            {
                return true;
            }
        }

        return false;
    }
}
