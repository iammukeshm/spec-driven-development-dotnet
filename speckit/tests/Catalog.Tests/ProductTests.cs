using System.Net;
using System.Net.Http.Json;
using Catalog.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Catalog.Tests;

public class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Catalog", $"Data Source={_dbPath}");
}

public class ProductTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Created_product_is_listed()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _client.PostAsJsonAsync("/products", new CreateProductRequest("Mug", 12.50m), ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/products", ct);
        Assert.Contains(products!, p => p.Name == "Mug");
    }
}
