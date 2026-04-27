namespace HardwareStore.Infrastructure;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Services;
using HardwareStore.Infrastructure.CosmosDb;
using HardwareStore.Infrastructure.Repositories;
using HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // CosmosDB
        services.AddSingleton<CosmosDbContext>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRetailerRepository, RetailerRepository>();
        services.AddScoped<ISearchRepository, SearchRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();

        // Services
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<INaturalLanguageService, NaturalLanguageService>();
        services.AddScoped<ISearchJobService, SearchJobService>();
        services.AddScoped<IRateLimitService, RateLimitService>();
        services.AddScoped<ISearchStatusNotifier, NoOpSearchStatusNotifier>();

        // Playwright browser service (singleton — one browser instance shared across all requests)
        services.AddSingleton<PlaywrightBrowserService>();

        // Retailer clients — feature flag selects Playwright or plain HttpClient
        var usePlaywright = configuration.GetValue<bool>("RetailerClients:UsePlaywright", defaultValue: false);
        if (usePlaywright)
        {
            services.AddScoped<IRetailerSearchClient, PlaywrightHomeDepotClient>();
            services.AddScoped<IRetailerSearchClient, PlaywrightLowesClient>();
        }
        else
        {
            services.AddScoped<IRetailerSearchClient, HomeDepotClient>();
            services.AddScoped<IRetailerSearchClient, LowesClient>();
        }

        // Background service
        services.AddHostedService<SearchBackgroundService>();

        // HTTP clients
        services.AddHttpClient("openai").SetHandlerLifetime(TimeSpan.FromMinutes(5));
        services.AddHttpClient("homedepot").SetHandlerLifetime(TimeSpan.FromMinutes(5));
        services.AddHttpClient("lowes").SetHandlerLifetime(TimeSpan.FromMinutes(5));

        return services;
    }
}