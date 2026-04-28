using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.RetailerClients;
using Moq;
using Moq.Protected;
using System.Net;

namespace HardwareStore.UnitTests;

public class HomeDepotClientTests
{
    // ─── ParseSearchResults (pure parsing, no HTTP) ───────────────────────

    private static string BuildHtml(string productsJson) => $@"<!DOCTYPE html>
<html><head></head><body>
<script id=""__NEXT_DATA__"" type=""application/json"">
{{
  ""props"": {{
    ""pageProps"": {{
      ""initialData"": {{
        ""searchReport"": {{
          ""keyword"": ""screws"",
          ""products"": {productsJson}
        }}
      }}
    }}
  }}
}}
</script>
</body></html>";

    [Fact]
    public void ParseSearchResults_WithValidProducts_ReturnsExpectedResults()
    {
        var html = BuildHtml(@"[
            {
                ""itemId"": ""123456"",
                ""description"": ""1/4 in. x 2 in. Hex Bolt (25-Pack)"",
                ""canonicalUrl"": ""/p/test/123456"",
                ""pricing"": { ""value"": 5.48 },
                ""media"": { ""images"": [{ ""url"": ""https://images.homedepot-static.com/test.jpg"" }] }
            },
            {
                ""itemId"": ""789012"",
                ""description"": ""Drywall Screws (5 lb.)"",
                ""canonicalUrl"": ""/p/screws/789012"",
                ""pricing"": { ""value"": 12.99 },
                ""media"": { ""images"": [] }
            }
        ]");

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Equal(2, results.Count);

        Assert.Equal("homedepot", results[0].RetailerId);
        Assert.Equal("Home Depot", results[0].RetailerName);
        Assert.Equal("1/4 in. x 2 in. Hex Bolt (25-Pack)", results[0].ProductTitle);
        Assert.Equal("https://www.homedepot.com/p/test/123456", results[0].ProductUrl);
        Assert.Equal(5.48m, results[0].Price);
        Assert.Equal("$5.48", results[0].PriceDisplay);
        Assert.Equal("https://images.homedepot-static.com/test.jpg", results[0].ImageUrl);
        Assert.Equal("123456", results[0].Sku);
        Assert.True(results[0].IsAvailable);

        Assert.Equal("Drywall Screws (5 lb.)", results[1].ProductTitle);
        Assert.Null(results[1].ImageUrl);
    }

    [Fact]
    public void ParseSearchResults_AbsoluteCanonicalUrl_UsedAsIs()
    {
        var html = BuildHtml(@"[
            {
                ""itemId"": ""999"",
                ""description"": ""Some Product"",
                ""canonicalUrl"": ""https://www.homedepot.com/p/something/999"",
                ""pricing"": { ""value"": 9.99 },
                ""media"": { ""images"": [] }
            }
        ]");

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Single(results);
        Assert.Equal("https://www.homedepot.com/p/something/999", results[0].ProductUrl);
    }

    [Fact]
    public void ParseSearchResults_ProductWithZeroPrice_IsSkipped()
    {
        var html = BuildHtml(@"[
            {
                ""itemId"": ""111"",
                ""description"": ""No Price Product"",
                ""canonicalUrl"": ""/p/no-price/111"",
                ""pricing"": { ""value"": 0 },
                ""media"": { ""images"": [] }
            }
        ]");

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_ProductWithMissingPricing_IsSkipped()
    {
        var html = BuildHtml(@"[
            {
                ""itemId"": ""222"",
                ""description"": ""No Pricing Field"",
                ""canonicalUrl"": ""/p/test/222"",
                ""media"": { ""images"": [] }
            }
        ]");

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_ProductWithEmptyDescription_IsSkipped()
    {
        var html = BuildHtml(@"[
            {
                ""itemId"": ""333"",
                ""description"": """",
                ""canonicalUrl"": ""/p/test/333"",
                ""pricing"": { ""value"": 7.99 },
                ""media"": { ""images"": [] }
            }
        ]");

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_LimitsToTenProducts()
    {
        var products = string.Join(",\n", Enumerable.Range(1, 15).Select(i =>
            $@"{{ ""itemId"": ""{i}"", ""description"": ""Product {i}"", ""canonicalUrl"": ""/p/{i}"", ""pricing"": {{ ""value"": {i}.99 }}, ""media"": {{ ""images"": [] }} }}"));
        var html = BuildHtml($"[{products}]");

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public void ParseSearchResults_WithNoNextDataScript_ReturnsEmpty()
    {
        const string html = "<html><body><p>No data here</p></body></html>";

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_WithMalformedJson_ReturnsEmpty()
    {
        const string html = @"<html><body>
<script id=""__NEXT_DATA__"" type=""application/json"">{ this is not valid json }</script>
</body></html>";

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_WithMissingProductsKey_ReturnsEmpty()
    {
        const string html = @"<html><body>
<script id=""__NEXT_DATA__"" type=""application/json"">
{ ""props"": { ""pageProps"": { ""initialData"": { ""searchReport"": { ""keyword"": ""screws"" } } } } }
</script></body></html>";

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_EmptyProductsArray_ReturnsEmpty()
    {
        var html = BuildHtml("[]");

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    // ─── NormalizeImageUrl ─────────────────────────────────────────────────

    [Fact]
    public void NormalizeImageUrl_AbsoluteHttpsUrl_ReturnedAsIs()
    {
        var url = "https://images.thdstatic.com/productImages/123/300/123.jpg";
        Assert.Equal(url, HomeDepotClient.NormalizeImageUrl(url));
    }

    [Fact]
    public void NormalizeImageUrl_AbsoluteHttpUrl_ReturnedAsIs()
    {
        var url = "http://images.thdstatic.com/productImages/123/300/123.jpg";
        Assert.Equal(url, HomeDepotClient.NormalizeImageUrl(url));
    }

    [Fact]
    public void NormalizeImageUrl_ProtocolRelativeUrl_PrependedWithHttps()
    {
        Assert.Equal(
            "https://images.thdstatic.com/productImages/123/300/123.jpg",
            HomeDepotClient.NormalizeImageUrl("//images.thdstatic.com/productImages/123/300/123.jpg"));
    }

    [Fact]
    public void NormalizeImageUrl_RelativePath_PrependedWithCdnBase()
    {
        Assert.Equal(
            "https://images.thdstatic.com/productImages/123/300/123.jpg",
            HomeDepotClient.NormalizeImageUrl("/productImages/123/300/123.jpg"));
    }

    [Fact]
    public void NormalizeImageUrl_Null_ReturnsNull()
    {
        Assert.Null(HomeDepotClient.NormalizeImageUrl(null));
    }

    [Fact]
    public void NormalizeImageUrl_EmptyString_ReturnsNull()
    {
        Assert.Null(HomeDepotClient.NormalizeImageUrl(""));
    }

    [Fact]
    public void ParseSearchResults_RelativeImageUrl_IsNormalized()
    {
        var html = BuildHtml(@"[
            {
                ""itemId"": ""abc"",
                ""description"": ""Some Product"",
                ""canonicalUrl"": ""/p/some/abc"",
                ""pricing"": { ""value"": 9.99 },
                ""media"": { ""images"": [{ ""url"": ""/productImages/abc/300/abc.jpg"" }] }
            }
        ]");

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Single(results);
        Assert.Equal("https://images.thdstatic.com/productImages/abc/300/abc.jpg", results[0].ImageUrl);
    }

    [Fact]
    public void ParseSearchResults_ProtocolRelativeImageUrl_IsNormalized()
    {
        var html = BuildHtml(@"[
            {
                ""itemId"": ""def"",
                ""description"": ""Another Product"",
                ""canonicalUrl"": ""/p/another/def"",
                ""pricing"": { ""value"": 5.00 },
                ""media"": { ""images"": [{ ""url"": ""//images.thdstatic.com/productImages/def/300/def.jpg"" }] }
            }
        ]");

        var results = HomeDepotClient.ParseSearchResults(html);

        Assert.Single(results);
        Assert.Equal("https://images.thdstatic.com/productImages/def/300/def.jpg", results[0].ImageUrl);
    }

    // ─── SearchProductAsync – HTTP failure handling ───────────────────────

    private static HomeDepotClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("homedepot")).Returns(httpClient);
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<HomeDepotClient>>();
        return new HomeDepotClient(factory.Object, logger.Object);
    }

    [Fact]
    public async Task SearchProductAsync_WhenHttpThrows_ReturnsEmptyList()
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
    public async Task SearchProductAsync_WhenResponseContainsProducts_ReturnsParsedResults()
    {
        var html = BuildHtml(@"[
            {
                ""itemId"": ""42"",
                ""description"": ""Wood Screw 1-1/2 in."",
                ""canonicalUrl"": ""/p/wood-screw/42"",
                ""pricing"": { ""value"": 3.99 },
                ""media"": { ""images"": [] }
            }
        ]");

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            });

        var client = CreateClient(handler.Object);
        var results = await client.SearchProductAsync("wood screw", new Retailer());

        Assert.Single(results);
        Assert.Equal("Wood Screw 1-1/2 in.", results[0].ProductTitle);
        Assert.Equal(3.99m, results[0].Price);
    }
}
