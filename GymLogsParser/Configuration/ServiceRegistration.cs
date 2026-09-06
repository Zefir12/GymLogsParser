using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace GymLogsParser.Configuration;

public static class ServiceRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices(IConfiguration configuration)
        {
            services.AddHttpClient();
            return services;
        }
    }
}