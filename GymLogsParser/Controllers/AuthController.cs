using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login(string? returnUrl, [FromServices] IConfiguration config)
    {
        var path = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        var frontend = config["Auth:FrontendUrl"]?.TrimEnd('/') ?? "";
        return Challenge(new AuthenticationProperties { RedirectUri = frontend + path },
            OpenIdConnectDefaults.AuthenticationScheme);
    }
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = User.FindFirst("sub")?.Value,
        email = User.FindFirst("email")?.Value,
        name = User.FindFirst("name")?.Value,
        roles = User.FindAll("roles").Select(c => c.Value)
    });

    [AllowAnonymous, HttpPost("logout")]
    public IActionResult Logout([FromServices] IConfiguration config)
    {
        var frontend = config["Auth:FrontendUrl"]?.TrimEnd('/') ?? "";

        // Already signed out locally: nothing to send to the IdP.
        if (User.Identity?.IsAuthenticated != true)
            return Redirect(frontend + "/signed-out");

        // Same origin as the registered redirect_uri, so Authestra accepts it.
        var postLogout = $"{Request.Scheme}://{Request.Host}/api/auth/signed-out";

        return SignOut(
            new AuthenticationProperties { RedirectUri = postLogout },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }
    
    [AllowAnonymous, HttpGet("signed-out")]
    public IActionResult SignedOut([FromServices] IConfiguration config)
    {
        var frontend = config["Auth:FrontendUrl"]?.TrimEnd('/') ?? "";
        return Redirect(frontend + "/signed-out");
    }
}