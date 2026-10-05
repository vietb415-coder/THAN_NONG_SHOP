using System.Text.Json;

namespace THAN_NONG_SHOP.Services;

// Guest order access is tied to the server session, never just a supplied order id.
public static class GuestOrderAccess
{
    private const string Key="GuestOrderIds";
    private static List<int> Read(HttpContext http) =>
        JsonSerializer.Deserialize<List<int>>(http.Session.GetString(Key) ?? "[]") ?? [];
    public static bool Contains(HttpContext http,int id)=>Read(http).Contains(id);
    public static void Grant(HttpContext http,int id)
    {
        var ids=Read(http);ids.Add(id);
        http.Session.SetString(Key,JsonSerializer.Serialize(ids.Distinct().TakeLast(20)));
    }
}
