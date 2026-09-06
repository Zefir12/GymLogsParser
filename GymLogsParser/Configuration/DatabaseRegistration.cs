using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GymLogsParser.Configuration;

public static class DatabaseRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDatabase(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Default");

            services.AddPooledDbContextFactory<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped(sp =>
                sp.GetRequiredService<IDbContextFactory<AppDbContext>>()
                    .CreateDbContext());

            return services;
        }
    }
}