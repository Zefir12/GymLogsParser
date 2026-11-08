using Core.Configuration;
using GymLog.Api.AI;
using Infrastructure.Interfaces;
using Infrastructure.Services;

namespace GymLogsParser.Configuration;

public static class ServiceRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices(IConfiguration configuration)
        {
            services.AddMemoryCache();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IExerciseCatalogService, ExerciseCatalogService>();
            services.AddScoped<IMuscleTargetService, MuscleTargetService>();
            services.AddScoped<IBodyweightService, BodyweightService>();
            services.AddScoped<IHevyImportService, HevyImportService>();
            services.AddSingleton<ILegacyDataClaimService, LegacyDataClaimService>();
            services.AddHttpClient<IDeepSeekService, DeepSeekService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DeepSeekOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(90);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
            });
            return services;
        }
    }
}