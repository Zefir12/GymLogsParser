using Microsoft.AspNetCore.Authorization;

namespace GymLogsParser.Configuration;

public static class AuthorizationRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppAuthorization(IWebHostEnvironment environment)
        {
            services.AddAuthorization(options =>
            {
                if (!environment.IsDevelopment())
                    options.FallbackPolicy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
            });
            return services;
        }
    }
}