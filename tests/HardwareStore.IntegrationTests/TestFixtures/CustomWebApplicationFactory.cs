using HardwareStore.Core.Interfaces;
using HardwareStore.Infrastructure.CosmosDb;
using HardwareStore.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HardwareStore.IntegrationTests.TestFixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public InMemoryUserRepository UserRepository { get; } = new();
    public InMemoryRetailerRepository RetailerRepository { get; } = new();
    public InMemorySearchRepository SearchRepository { get; } = new();
    public InMemoryReportRepository ReportRepository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove the CosmosDB singleton so startup never tries to connect
            services.RemoveAll<CosmosDbContext>();

            // Prevent the background search processor from running during tests
            services.RemoveAll<IHostedService>();

            // Replace all CosmosDB-backed repositories with in-memory equivalents
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IRetailerRepository>();
            services.RemoveAll<ISearchRepository>();
            services.RemoveAll<IReportRepository>();
            services.RemoveAll<IEmailService>();
            services.RemoveAll<INaturalLanguageService>();
            services.RemoveAll<ISearchStatusNotifier>();

            services.AddSingleton<IUserRepository>(UserRepository);
            services.AddSingleton<IRetailerRepository>(RetailerRepository);
            services.AddSingleton<ISearchRepository>(SearchRepository);
            services.AddSingleton<IReportRepository>(ReportRepository);
            services.AddSingleton<IEmailService, NoOpEmailService>();
            services.AddSingleton<INaturalLanguageService, FakeNaturalLanguageService>();
            services.AddSingleton<ISearchStatusNotifier, NoOpSearchStatusNotifier>();
        });
    }
}
