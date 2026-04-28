using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.RetailerClients;
using Moq;
using Moq.Protected;
using System.Net;

namespace HardwareStore.UnitTests;

public class LowesClientTests
{
    // ─── ParseSearchResults (pure parsing, no HTTP) ───────────────────────

    private static string BuildHtml(string productsJson, string path = "searchModel") => $@"<!DOCTYPE html>
<html><head></head><body>
<script id=""__NEXT_DATA__"" type=""application/json"">
{{
  ""props"": {{
    ""pageProps"": {{
      ""{path}"": {{
        ""products"": {productsJson}
      }}
    }}
  }}
}}
</script>
</body></html>";

    [Fact]
    public void ParseSearchResults_WithSearchModel_ReturnsExpectedResults()
    {
        var html = BuildHtml(@"[
            {
                ""itemid"": ""5013816171"",
                ""description"": ""1/4-in x 2-in Coarse Thread Hex Bolt"",
                ""productUrl"": ""/pd/test/5013816171"",
                ""pricing"": { ""amount"": 2.98 },
                ""imageUrl"": ""https://mobileimages.lowes.com/test.jpg""
            },
            {
                ""itemid"": ""1234567"",
                ""description"": ""3/8-in x 3-in Zinc Hex Bolt (5-Pack)"",
                ""productUrl"": ""/pd/bolt/1234567"",
                ""pricing"": { ""amount"": 8.47 },
                ""imageUrl"": null
            }
        ]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Equal(2, results.Count);

        Assert.Equal("lowes", results[0].RetailerId);
        Assert.Equal("Lowe's", results[0].RetailerName);
        Assert.Equal("1/4-in x 2-in Coarse Thread Hex Bolt", results[0].ProductTitle);
        Assert.Equal("https://www.lowes.com/pd/test/5013816171", results[0].ProductUrl);
        Assert.Equal(2.98m, results[0].Price);
        Assert.Equal("$2.98", results[0].PriceDisplay);
        Assert.Equal("https://mobileimages.lowes.com/test.jpg", results[0].ImageUrl);
        Assert.Equal("5013816171", results[0].Sku);
        Assert.True(results[0].IsAvailable);

        Assert.Equal("3/8-in x 3-in Zinc Hex Bolt (5-Pack)", results[1].ProductTitle);
    }

    [Fact]
    public void ParseSearchResults_WithSearchData_FallbackPath_ReturnsResults()
    {
        var html = BuildHtml(@"[
            {
                ""productId"": ""9999"",
                ""description"": ""Drywall Screw"",
                ""productUrl"": ""/pd/drywall/9999"",
                ""pricing"": { ""amount"": 4.99 }
            }
        ]", "searchData");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Single(results);
        Assert.Equal("Drywall Screw", results[0].ProductTitle);
        Assert.Equal("9999", results[0].Sku);
    }

    [Fact]
    public void ParseSearchResults_PricingWithValueField_ParsesCorrectly()
    {
        var html = BuildHtml(@"[
            {
                ""itemid"": ""777"",
                ""description"": ""Lag Screw"",
                ""productUrl"": ""/pd/lag/777"",
                ""pricing"": { ""value"": 6.49 }
            }
        ]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Single(results);
        Assert.Equal(6.49m, results[0].Price);
    }

    [Fact]
    public void ParseSearchResults_AbsoluteProductUrl_UsedAsIs()
    {
        var html = BuildHtml(@"[
            {
                ""itemid"": ""888"",
                ""description"": ""Carriage Bolt"",
                ""productUrl"": ""https://www.lowes.com/pd/carriage/888"",
                ""pricing"": { ""amount"": 1.58 }
            }
        ]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Single(results);
        Assert.Equal("https://www.lowes.com/pd/carriage/888", results[0].ProductUrl);
    }

    [Fact]
    public void ParseSearchResults_ProductWithZeroPrice_IsSkipped()
    {
        var html = BuildHtml(@"[
            {
                ""itemid"": ""000"",
                ""description"": ""Free Product"",
                ""productUrl"": ""/pd/free/000"",
                ""pricing"": { ""amount"": 0 }
            }
        ]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_ProductWithMissingPricing_IsSkipped()
    {
        var html = BuildHtml(@"[
            {
                ""itemid"": ""111"",
                ""description"": ""No Price"",
                ""productUrl"": ""/pd/no-price/111""
            }
        ]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_ProductWithEmptyDescription_IsSkipped()
    {
        var html = BuildHtml(@"[
            {
                ""itemid"": ""222"",
                ""description"": """",
                ""productUrl"": ""/pd/test/222"",
                ""pricing"": { ""amount"": 3.99 }
            }
        ]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_LimitsToTenProducts()
    {
        var products = string.Join(",\n", Enumerable.Range(1, 15).Select(i =>
            $@"{{ ""itemid"": ""{i}"", ""description"": ""Product {i}"", ""productUrl"": ""/pd/{i}"", ""pricing"": {{ ""amount"": {i}.99 }} }}"));
        var html = BuildHtml($"[{products}]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public void ParseSearchResults_WithNoNextDataScript_ReturnsEmpty()
    {
        const string html = "<html><body><p>No data here</p></body></html>";

        var results = LowesClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_WithMalformedJson_ReturnsEmpty()
    {
        const string html = @"<html><body>
<script id=""__NEXT_DATA__"" type=""application/json"">{ this is not valid json }</script>
</body></html>";

        var results = LowesClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_WithNoKnownProductsPath_ReturnsEmpty()
    {
        const string html = @"<html><body>
<script id=""__NEXT_DATA__"" type=""application/json"">
{ ""props"": { ""pageProps"": { ""someOtherKey"": {} } } }
</script></body></html>";

        var results = LowesClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResults_EmptyProductsArray_ReturnsEmpty()
    {
        var html = BuildHtml("[]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Empty(results);
    }

    // ─── BuildLowesSlug ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Moen Arbor Matte Black", "Moen-Arbor-Matte-Black")]
    [InlineData("1/4-in x 2-in Hex Bolt", "1-4-in-x-2-in-Hex-Bolt")]
    [InlineData("Product (5-Pack)", "Product-5-Pack")]
    [InlineData("Commercial/Residential Faucet", "Commercial-Residential-Faucet")]
    [InlineData("Single Word", "Single-Word")]
    public void BuildLowesSlug_ConvertsDescriptionToSlug(string description, string expected)
    {
        Assert.Equal(expected, LowesClient.BuildLowesSlug(description));
    }

    // ─── BuildProductUrl ──────────────────────────────────────────────────

    [Fact]
    public void BuildProductUrl_WithRelativeProductUrl_PrependsBaseUrl()
    {
        Assert.Equal(
            "https://www.lowes.com/pd/test/123",
            LowesClient.BuildProductUrl("/pd/test/123", "123", "Test Product"));
    }

    [Fact]
    public void BuildProductUrl_WithAbsoluteProductUrl_UsedAsIs()
    {
        Assert.Equal(
            "https://www.lowes.com/pd/test/123",
            LowesClient.BuildProductUrl("https://www.lowes.com/pd/test/123", "123", "Test Product"));
    }

    [Fact]
    public void BuildProductUrl_WithEmptyProductUrlAndSku_ConstructsFromSlugAndSku()
    {
        var result = LowesClient.BuildProductUrl("", "5000058723", "Moen Arbor Matte Black Faucet");
        Assert.Equal("https://www.lowes.com/pd/Moen-Arbor-Matte-Black-Faucet/5000058723", result);
    }

    [Fact]
    public void BuildProductUrl_WithNullProductUrlAndSku_ConstructsFromSlugAndSku()
    {
        var result = LowesClient.BuildProductUrl("", "9999", "DEWALT 20V MAX Drill");
        Assert.Equal("https://www.lowes.com/pd/DEWALT-20V-MAX-Drill/9999", result);
    }

    [Fact]
    public void BuildProductUrl_WithEmptyProductUrlAndNoSku_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, LowesClient.BuildProductUrl("", null, "Some Product"));
    }

    [Fact]
    public void ParseSearchResults_WhenProductUrlMissing_ConstructsFromDescriptionAndSku()
    {
        var html = BuildHtml(@"[
            {
                ""itemid"": ""5000058723"",
                ""description"": ""Moen Arbor Matte Black Faucet"",
                ""pricing"": { ""amount"": 209.00 },
                ""imageUrl"": ""https://mobileimages.lowes.com/test.jpg""
            }
        ]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Single(results);
        Assert.Equal("https://www.lowes.com/pd/Moen-Arbor-Matte-Black-Faucet/5000058723", results[0].ProductUrl);
    }

    [Fact]
    public void ParseSearchResults_WhenProductUrlIsNull_ConstructsFromDescriptionAndSku()
    {
        var html = BuildHtml(@"[
            {
                ""itemid"": ""1234567"",
                ""description"": ""DEWALT 20V Drill"",
                ""productUrl"": null,
                ""pricing"": { ""amount"": 99.00 }
            }
        ]");

        var results = LowesClient.ParseSearchResults(html);

        Assert.Single(results);
        Assert.Equal("https://www.lowes.com/pd/DEWALT-20V-Drill/1234567", results[0].ProductUrl);
    }

    // ─── SearchProductAsync – HTTP failure handling ───────────────────────

    private static LowesClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("lowes")).Returns(httpClient);
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LowesClient>>();
        return new LowesClient(factory.Object, logger.Object);
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
                ""itemid"": ""55"",
                ""description"": ""Framing Nail 3-1/2 in."",
                ""productUrl"": ""/pd/nail/55"",
                ""pricing"": { ""amount"": 15.97 }
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
        var results = await client.SearchProductAsync("framing nail", new Retailer());

        Assert.Single(results);
        Assert.Equal("Framing Nail 3-1/2 in.", results[0].ProductTitle);
        Assert.Equal(15.97m, results[0].Price);
    }
}
