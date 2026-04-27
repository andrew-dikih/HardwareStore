using HardwareStore.Core.Interfaces;
using HardwareStore.Infrastructure;
using HardwareStore.Infrastructure.RetailerClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HardwareStore.UnitTests;

public class DependencyInjectionTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void AddInfrastructure_WithSerpApiDisabled_RegistersPlainHttpClients()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        var config = BuildConfig(new() { ["RetailerClients:UseSerpApi"] = "false" });

        services.AddInfrastructure(config);

        var descriptors = services.Where(d => d.ServiceType == typeof(IRetailerSearchClient)).ToList();
        Assert.Equal(2, descriptors.Count);
        Assert.Contains(descriptors, d => d.ImplementationType == typeof(HomeDepotClient));
        Assert.Contains(descriptors, d => d.ImplementationType == typeof(LowesClient));
    }

    [Fact]
    public void AddInfrastructure_WithSerpApiEnabled_RegistersSerpApiClients()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        var config = BuildConfig(new()
        {
            ["RetailerClients:UseSerpApi"] = "true",
            ["SerpApi:ApiKey"] = "test",
            ["SerpApi:BaseUrl"] = "https://serpapi.com"
        });

        services.AddInfrastructure(config);

        var descriptors = services.Where(d => d.ServiceType == typeof(IRetailerSearchClient)).ToList();
        Assert.Equal(2, descriptors.Count);
        Assert.Contains(descriptors, d => d.ImplementationType == typeof(SerpApiHomeDepotClient));
        Assert.Contains(descriptors, d => d.ImplementationType == typeof(SerpApiLowesClient));
    }
}
