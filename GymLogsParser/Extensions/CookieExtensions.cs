namespace GymLogsParser.Extensions;

public static class CookieExtensions
{
    public static void SetAuthCookies(
        this HttpResponse response,
        string accessToken,
        string refreshToken,
        IConfiguration config)
    {
        response.Cookies.Append(
            "accessToken",
            accessToken,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = !config.GetValue<bool>("Dev"),
                SameSite = config.GetValue<bool>("Dev") ? SameSiteMode.Lax : SameSiteMode.None,
                Expires = DateTime.UtcNow.AddMinutes(15),
                Path = "/"
            }
        );

        response.Cookies.Append(
            "refreshToken",
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !config.GetValue<bool>("Dev"),
                SameSite = config.GetValue<bool>("Dev") ? SameSiteMode.Lax : SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(30),
                Path = "/"
            }
        );
    }


    public static void DeleteAuthCookies(
        this HttpResponse response)
    {
        response.Cookies.Delete("accessToken");
        response.Cookies.Delete("refreshToken");
    }
}