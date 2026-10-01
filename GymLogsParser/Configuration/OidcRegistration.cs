using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace GymLogsParser.Configuration;

public static class OidcRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddOidcAuthentication(IConfiguration configuration, IWebHostEnvironment env)
        {
            var auth = configuration.GetSection("Auth");

            services
                .AddAuthentication(o =>
                {
                    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    o.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme; // 401 for API calls
                })
                .AddCookie(o =>
                {
                    o.Cookie.Name = "gymlogs.session";
                    o.Cookie.HttpOnly = true;
                    o.ExpireTimeSpan = TimeSpan.FromHours(4);
                    o.SlidingExpiration = false;   // caps how stale the roles claim can get
                    o.Cookie.SameSite = SameSiteMode.Lax;
                    o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
                    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, o =>
                {
                    o.Authority = auth["Authority"];            // must equal Authestra's ISSUER exactly
                    o.ClientId = auth["ClientId"];
                    o.ClientSecret = auth["ClientSecret"];
                    o.RequireHttpsMetadata = !env.IsDevelopment();

                    o.ResponseType = "code";
                    o.ResponseMode = "query";
                    o.UsePkce = true;
                    o.SaveTokens = false;
                    o.MapInboundClaims = false;
                    o.GetClaimsFromUserInfoEndpoint = false;

                    o.Scope.Clear();
                    o.Scope.Add("openid");
                    o.Scope.Add("profile");
                    o.Scope.Add("email");

                    o.TokenValidationParameters.NameClaimType = "name";
                    o.TokenValidationParameters.RoleClaimType = "roles";
                    o.Events.OnRemoteFailure = ctx => {
                        var err = ctx.Request.Query["error"].ToString();
                        var frontend = auth["FrontendUrl"]?.TrimEnd('/') ?? "";
                        ctx.Response.Redirect($"{frontend}/auth/error?code={Uri.EscapeDataString(err)}");
                        ctx.HandleResponse();
                        return Task.CompletedTask;
                    };
                    o.Events.OnTokenValidated = ctx =>
                    {
                        var idToken = ctx.TokenEndpointResponse?.IdToken ?? ctx.ProtocolMessage?.IdToken;
                        if (idToken is not null)
                            ctx.Properties!.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
                        return Task.CompletedTask;
                    };
                });

            return services;
        }
    }
}