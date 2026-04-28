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

public class SerpApiLowesClientTests
{
    private static SerpApiLowesClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("serpapi")).Returns(httpClient);

        var settings = Options.Create(new SerpApiSettings
        {
            ApiKey = "test-key",
            BaseUrl = "https://serpapi.com"
        });

        var logger = new Mock<ILogger<SerpApiLowesClient>>();
        return new SerpApiLowesClient(factory.Object, settings, logger.Object);
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
    public void RetailerId_IsLowes()
    {
        var client = CreateClient(OkJson("{}"));
        Assert.Equal("lowes", client.RetailerId);
    }

    [Fact]
    public void RetailerName_IsLowes()
    {
        var client = CreateClient(OkJson("{}"));
        Assert.Equal("Lowe's", client.RetailerName);
    }

    // ── ParsePrice ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("$9.99", 9.99)]
    [InlineData("$24.98", 24.98)]
    [InlineData("$1,299.00", 1299.00)]
    public void ParsePrice_WithSimplePrice_ParsesCorrectly(string input, decimal expected)
    {
        Assert.Equal(expected, SerpApiLowesClient.ParsePrice(input));
    }

    [Theory]
    [InlineData("$24.98 - $39.98", 24.98)]
    [InlineData("$10.00 - $20.00", 10.00)]
    public void ParsePrice_WithPriceRange_ReturnsFirstValue(string input, decimal expected)
    {
        Assert.Equal(expected, SerpApiLowesClient.ParsePrice(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParsePrice_WithNullOrEmpty_ReturnsZero(string? input)
    {
        Assert.Equal(0m, SerpApiLowesClient.ParsePrice(input));
    }

    [Theory]
    [InlineData("not a price")]
    [InlineData("$")]
    public void ParsePrice_WithUnparseable_ReturnsZero(string input)
    {
        Assert.Equal(0m, SerpApiLowesClient.ParsePrice(input));
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchProductAsync_WithLowesResultBySource_ReturnsMappedResult()
    {
        var json = """
            {
                "shopping_results": [
                    {
                        "title": "DEWALT 20V Drill",
                        "price": "$99.00",
                        "link": "https://www.lowes.com/pd/dewalt/123",
                        "thumbnail": "https://lowes.com/img/thumb.jpg",
                        "source": "Lowe's"
                    }
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("drill", new Retailer());

        Assert.Single(results);
        Assert.Equal("lowes", results[0].RetailerId);
        Assert.Equal("Lowe's", results[0].RetailerName);
        Assert.Equal("DEWALT 20V Drill", results[0].ProductTitle);
        Assert.Equal("https://www.lowes.com/pd/dewalt/123", results[0].ProductUrl);
        Assert.Equal(99.00m, results[0].Price);
        Assert.Equal("$99.00", results[0].PriceDisplay);
        Assert.Equal("https://lowes.com/img/thumb.jpg", results[0].ImageUrl);
        Assert.True(results[0].IsAvailable);
    }

    [Fact]
    public async Task SearchProductAsync_WithLowesResultByLink_ReturnsMappedResult()
    {
        var json = """
            {
                "shopping_results": [
                    {
                        "title": "DEWALT Drill",
                        "price": "$89.00",
                        "link": "https://www.lowes.com/pd/dewalt/456",
                        "source": "Some Store"
                    }
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("drill", new Retailer());

        Assert.Single(results);
        Assert.Equal("DEWALT Drill", results[0].ProductTitle);
    }

    [Fact]
    public async Task SearchProductAsync_WithProductLink_UsesProductLinkOverLink()
    {
        var json = """
            {
                "shopping_results": [
                    {
                        "title": "Moen Faucet",
                        "price": "$209.00",
                        "link": "https://www.google.com/shopping/product/1/specs",
                        "product_link": "https://www.lowes.com/pd/Moen-Faucet/5000058723",
                        "source": "Lowe's"
                    }
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("faucet", new Retailer());

        Assert.Single(results);
        Assert.Equal("https://www.lowes.com/pd/Moen-Faucet/5000058723", results[0].ProductUrl);
    }

    [Fact]
    public async Task SearchProductAsync_WithNullLinkAndProductLink_UsesProductLink()
    {
        var json = """
            {
                "shopping_results": [
                    {
                        "title": "Moen Faucet",
                        "price": "$209.00",
                        "product_link": "https://www.lowes.com/pd/Moen-Faucet/5000058723",
                        "source": "Lowe's"
                    }
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("faucet", new Retailer());

        Assert.Single(results);
        Assert.Equal("https://www.lowes.com/pd/Moen-Faucet/5000058723", results[0].ProductUrl);
    }

    [Fact]
    public async Task SearchProductAsync_WithNullLinkAndNoProductLink_SetsEmptyProductUrl()
    {
        var json = """
            {
                "shopping_results": [
                    {
                        "title": "Moen Faucet",
                        "price": "$209.00",
                        "source": "Lowe's"
                    }
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("faucet", new Retailer());

        Assert.Single(results);
        Assert.Equal(string.Empty, results[0].ProductUrl);
    }

    [Fact]
    public async Task SearchProductAsync_FiltersOutNonLowesResults()
    {
        var json = """
            {
                "shopping_results": [
                    {"title":"HD Product","price":"$10.00","link":"https://www.homedepot.com/p/hd/1","source":"Home Depot"},
                    {"title":"Lowes Product","price":"$12.00","link":"https://www.lowes.com/pd/lowes/2","source":"Lowe's"},
                    {"title":"Amazon Product","price":"$8.00","link":"https://www.amazon.com/dp/3","source":"Amazon"}
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("drill", new Retailer());

        Assert.Single(results);
        Assert.Equal("Lowes Product", results[0].ProductTitle);
    }

    [Fact]
    public async Task SearchProductAsync_LimitsToTenResults()
    {
        var items = string.Join(",", Enumerable.Range(1, 15).Select(i =>
            $$"""{"title":"Product {{i}}","price":"${{i}}.99","link":"https://www.lowes.com/pd/p/{{i}}","source":"Lowe's"}"""));
        var json = $$"""{"shopping_results":[{{items}}]}""";

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("drill", new Retailer());

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task SearchProductAsync_WithRangePrice_UsesFirstValue()
    {
        var json = """
            {
                "shopping_results": [
                    {"title":"Variable Widget","price":"$24.98 - $39.98","link":"https://www.lowes.com/pd/w/1","source":"Lowe's"}
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("widget", new Retailer());

        Assert.Single(results);
        Assert.Equal(24.98m, results[0].Price);
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchProductAsync_ProductWithZeroPrice_IsFiltered()
    {
        var json = """
            {
                "shopping_results": [
                    {"title":"Broken Price","price":"not-a-price","link":"https://www.lowes.com/pd/b/1","source":"Lowe's"}
                ]
            }
            """;

        var client = CreateClient(OkJson(json));
        var results = await client.SearchProductAsync("widget", new Retailer());

        Assert.Empty(results);
    }

    // ── Null / empty responses ────────────────────────────────────────────────

    [Fact]
    public async Task SearchProductAsync_WhenShoppingResultsIsNull_ReturnsEmpty()
    {
        var client = CreateClient(OkJson("""{"shopping_results":null}"""));
        var results = await client.SearchProductAsync("drill", new Retailer());
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchProductAsync_WhenShoppingResultsKeyMissing_ReturnsEmpty()
    {
        var client = CreateClient(OkJson("{}"));
        var results = await client.SearchProductAsync("drill", new Retailer());
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchProductAsync_WhenShoppingResultsEmpty_ReturnsEmpty()
    {
        var client = CreateClient(OkJson("""{"shopping_results":[]}"""));
        var results = await client.SearchProductAsync("drill", new Retailer());
        Assert.Empty(results);
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
        var results = await client.SearchProductAsync("drill", new Retailer());

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchProductAsync_WhenServerReturns500_ReturnsEmpty()
    {
        var client = CreateClient(JsonHandler(HttpStatusCode.InternalServerError, "{}"));
        var results = await client.SearchProductAsync("drill", new Retailer());
        Assert.Empty(results);
    }
}
