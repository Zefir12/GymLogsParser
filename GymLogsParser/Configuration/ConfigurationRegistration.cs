using System.Text;
using Core.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace GymLogsParser.Configuration;




public static class ConfigurationRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddConfigurationRegistration(IConfiguration configuration)
        {
            services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
            services.Configure<DeepSeekOptions>( configuration.GetSection("DeepSeek"));
            return services;
        }
    }
}