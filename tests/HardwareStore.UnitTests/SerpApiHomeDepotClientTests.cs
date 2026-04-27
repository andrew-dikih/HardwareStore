using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.RetailerClients;
using HardwareStore.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace HardwareStore.UnitTests;

public class SerpApiHomeDepotClientTests
{
    private static SerpApiHomeDepotClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("serpapi")).Returns(httpClient);

        var settings = Options.Create(new SerpApiSettings
        {
            ApiKey = "test-key",
            BaseUrl = "https://serpapi.com"
        });

        var logger = new Mock<ILogger<SerpApiHomeDepotClient>>();
        return new SerpApiHomeDepotClient(factory.Object, settings, logger.Object);
    }

    private static HttpMessageHandler OkJson(string json) =>
        JsonHandler(HttpStatusCode.OK, json);

    private static HttpMessageHandler JsonHandler(HttpStatusCode status, string json)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        return handler.Object;
    }

    // ── Identity ─────────────────────────────────────────────────────────────

    [Fact]
    public void RetailerId_IsHomeDepot()
    {
        var client = CreateClient(OkJson("{}"));
        Assert.Equal("homedepot", client.RetailerId);
    }

    [Fact]
    public void RetailerName_IsHomeDepot()
    {
        var client = CreateClient(OkJson("{}"));
        Assert.Equal("Home Depot", client.RetailerName);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchProductAsync_WithValidProducts_ReturnsMappedResults()
    {
        var json = """
            {
                "products": [
                    {
                        "title": "Duck Tape 1.88 in. x 60 yd.",
                        "price": 7.98,
                        "link": "https://www.homedepot.com/p/duck/123",
                        "thumbnail": "https://images.homedepot-static.com/thumb.jpg",
                        "product_id": "123"
                    }
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("duct tape", new Retailer());

        Assert.Single(results);
        Assert.Equal("homedepot", results[0].RetailerId);
        Assert.Equal("Home Depot", results[0].RetailerName);
        Assert.Equal("Duck Tape 1.88 in. x 60 yd.", results[0].ProductTitle);
        Assert.Equal("https://www.homedepot.com/p/duck/123", results[0].ProductUrl);
        Assert.Equal(7.98m, results[0].Price);
        Assert.Equal("$7.98", results[0].PriceDisplay);
        Assert.Equal("https://images.homedepot-static.com/thumb.jpg", results[0].ImageUrl);
        Assert.Equal("123", results[0].Sku);
        Assert.True(results[0].IsAvailable);
    }

    [Fact]
    public async Task SearchProductAsync_WithMultipleProducts_ReturnsAll()
    {
        var products = string.Join(",", Enumerable.Range(1, 3).Select(i =>
            $$"""{"title":"Product {{i}}","price":{{i}}.99,"link":"https://homedepot.com/p/{{i}}","product_id":"{{i}}"}"""));
        var json = $$"""{"products":[{{products}}]}""";

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("screws", new Retailer());

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task SearchProductAsync_LimitsToTenResults()
    {
        var products = string.Join(",", Enumerable.Range(1, 15).Select(i =>
            $$"""{"title":"Product {{i}}","price":{{i}}.99,"link":"https://homedepot.com/p/{{i}}","product_id":"{{i}}"}"""));
        var json = $$"""{"products":[{{products}}]}""";

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("screws", new Retailer());

        Assert.Equal(10, results.Count);
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchProductAsync_ProductWithZeroPrice_IsFiltered()
    {
        var json = """
            {
                "products": [
                    {"title":"Free Product","price":0,"link":"https://homedepot.com/p/free","product_id":"0"}
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("free", new Retailer());

        Assert.Empty(results);
    }

    // ── Null / empty responses ────────────────────────────────────────────────

    [Fact]
    public async Task SearchProductAsync_WhenProductsIsNull_ReturnsEmpty()
    {
        var client = CreateClient(OkJson("""{"products":null}"""));
        var results = await client.SearchProductAsync("screws", new Retailer());
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchProductAsync_WhenProductsKeyMissing_ReturnsEmpty()
    {
        var client = CreateClient(OkJson("{}"));
        var results = await client.SearchProductAsync("screws", new Retailer());
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchProductAsync_WhenProductsEmpty_ReturnsEmpty()
    {
        var client = CreateClient(OkJson("""{"products":[]}"""));
        var results = await client.SearchProductAsync("screws", new Retailer());
        Assert.Empty(results);
    }

    // ── Null optional fields ──────────────────────────────────────────────────

    [Fact]
    public async Task SearchProductAsync_WithNullThumbnail_SetsImageUrlToNull()
    {
        var json = """
            {
                "products": [
                    {"title":"Widget","price":5.00,"link":"https://homedepot.com/p/w","thumbnail":null,"product_id":"w1"}
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("widget", new Retailer());

        Assert.Single(results);
        Assert.Null(results[0].ImageUrl);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchProductAsync_WhenHttpThrows_ReturnsEmpty()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network error"));

        var client = CreateClient(handler.Object);
        var results = await client.SearchProductAsync("screws", new Retailer());

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchProductAsync_WhenServerReturns500_ReturnsEmpty()
    {
        var client = CreateClient(JsonHandler(HttpStatusCode.InternalServerError, "{}"));
        var results = await client.SearchProductAsync("screws", new Retailer());
        Assert.Empty(results);
    }
}
