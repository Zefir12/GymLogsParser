namespace GymLogsParser.Configuration;

public static class CorsPolicies
{
    public const string Dev = "DevCors";
}

public static class CorsRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCorsPolicies(IConfiguration configuration)
        {
            var corsOrigins = configuration["Frontend:Cors"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            services.AddCors(options =>
            {
                options.AddPolicy(
                    CorsPolicies.Dev,
                    policy =>
                    {
                        policy
                            .WithOrigins(corsOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    }
                );
            });
            return services;
        }
    }
}