using Microsoft.AspNetCore.Authorization;

namespace GymLogsParser.Configuration;

public static class AuthorizationRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppAuthorization(IWebHostEnvironment environment)
        {
            var builder = services.AddAuthorizationBuilder();

            if (!environment.IsDevelopment())
            {
                builder.SetFallbackPolicy(
                    new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build());
            }

            builder.AddPolicy("admin", policy =>
                policy.RequireRole("admin"));

            return services;
        }
    }
}