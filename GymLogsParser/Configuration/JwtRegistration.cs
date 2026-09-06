using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
namespace GymLogsParser.Configuration;

public static class JwtRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddJwtAuthentication(IConfiguration configuration)
        {
            var jwtSection = configuration.GetSection("Jwt");
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            context.Token = context.Request.Cookies["accessToken"];
                            return Task.CompletedTask;
                        }
                    }; // this makes it takes jwt from cookie instead of Authorization: bearer

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuer = jwtSection["Issuer"],
                        ValidAudience = jwtSection["Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!))
                    };
                });
            return services;
        }
    }
}